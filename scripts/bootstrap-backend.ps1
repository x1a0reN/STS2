param(
  [string]$ApiBaseUrl = "https://api.gongdou.games",
  [string]$AdminToken = $env:GONGDOU_ADMIN_TOKEN,
  [string]$ModZipPath = "",
  [switch]$SkipServerModCleanup,
  [switch]$SkipServerModCleanupTimer
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
if ([string]::IsNullOrWhiteSpace($AdminToken)) {
  $AdminToken = [Environment]::GetEnvironmentVariable("GONGDOU_ADMIN_TOKEN", "User")
}
if ([string]::IsNullOrWhiteSpace($AdminToken)) {
  throw "GONGDOU_ADMIN_TOKEN is not configured. Provide -AdminToken or set HKCU\Environment\GONGDOU_ADMIN_TOKEN."
}

$headers = @{ Authorization = "Bearer $AdminToken" }

function Invoke-GongdouApi {
  param(
    [string]$Method,
    [string]$Path,
    [object]$Body = $null
  )

  $uri = "$ApiBaseUrl$Path"
  if ($null -eq $Body) {
    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
  }

  $json = $Body | ConvertTo-Json -Depth 32
  $utf8 = [System.Text.UTF8Encoding]::new($false)
  $bytes = $utf8.GetBytes($json)
  return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json; charset=utf-8" -Body $bytes
}

function Get-ApiData($response) {
  if ($null -eq $response) { return $null }
  if ($response.PSObject.Properties.Name -contains "data") { return $response.data }
  return $response
}

function Get-CreateId($response) {
  $data = Get-ApiData $response
  if ($data -is [int] -or $data -is [long]) { return [int]$data }
  if ($null -ne $data -and $data.PSObject.Properties.Name -contains "id") { return [int]$data.id }
  return [int]$data
}

function Get-PagedItems($data) {
  if ($null -eq $data) { return @() }
  if ($data -is [array]) { return @($data) }
  if ($data.PSObject.Properties.Name -contains "items") { return @($data.items) }
  return @($data)
}

function Convert-SizeToGiB([string]$value) {
  if ($value -notmatch '^([0-9.]+)([KMGTP]?)$') { return 0.0 }
  $number = [double]$Matches[1]
  switch ($Matches[2]) {
    "K" { return $number / 1024 / 1024 }
    "M" { return $number / 1024 }
    "G" { return $number }
    "T" { return $number * 1024 }
    "P" { return $number * 1024 * 1024 }
    default { return $number / 1024 / 1024 / 1024 }
  }
}

function Test-ServerDiskForPublish {
  $output = & ssh root@47.98.165.140 "df -h / /var/www/fightcommunity-api /var/www/gongdou.games 2>/dev/null || df -h /"
  $output | ForEach-Object { Write-Host $_ }
  $rootLine = @($output | Where-Object { $_ -match '\s/$' } | Select-Object -First 1)[0]
  if ([string]::IsNullOrWhiteSpace($rootLine)) {
    throw "Cannot parse root filesystem disk usage from df output."
  }

  $columns = @($rootLine -split '\s+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
  if ($columns.Count -lt 6 -or $columns[4] -notmatch '^(\d+)%$') {
    throw "Cannot parse root filesystem disk usage line: $rootLine"
  }

  $usedPercent = [int]$Matches[1]
  $availableGiB = Convert-SizeToGiB $columns[3]
  if ($usedPercent -ge 95 -or $availableGiB -lt 4.0) {
    throw "Server disk precheck failed: root usage ${usedPercent}%, available $([math]::Round($availableGiB, 2)) GiB."
  }
}

function Invoke-ModUpload([int]$GameId, [string]$Path) {
  if (-not (Test-Path -LiteralPath $Path)) {
    throw "Mod zip not found: $Path"
  }

  $client = [System.Net.Http.HttpClient]::new()
  $stream = $null
  $content = $null
  try {
    $client.Timeout = [TimeSpan]::FromMinutes(10)
    $client.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $AdminToken)
    $content = [System.Net.Http.MultipartFormDataContent]::new()
    $stream = [System.IO.File]::OpenRead($Path)
    $fileContent = [System.Net.Http.StreamContent]::new($stream)
    $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/zip")
    $content.Add($fileContent, "file", [System.IO.Path]::GetFileName($Path))
    $response = $client.PostAsync("$ApiBaseUrl/api/admin/games/$GameId/mod-upload", $content).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) {
      throw "MOD upload failed: HTTP $([int]$response.StatusCode) $body"
    }
    if ([string]::IsNullOrWhiteSpace($body)) { return $null }
    return $body | ConvertFrom-Json
  }
  finally {
    if ($content -ne $null) { $content.Dispose() }
    if ($stream -ne $null) { $stream.Dispose() }
    $client.Dispose()
  }
}

$gameName = "杀戮尖塔2"
$executableName = "SlayTheSpire2.exe"
$categoryName = "卡牌构筑式 Roguelike"
$channelName = "杀戮尖塔2"
$presetName = "尖塔残局（10关）"
$leaderboardTitle = "尖塔残局：十关连战"
$stage1Title = "尖塔残局 01：石化狂信徒的第一课"
$leaderboardTitle2 = "尖塔残局 02：药水池与护甲门槛"
$stageLeaderboards = @(
  @{ Stage = 1; Title = $stage1Title; PuzzleId = "difficulty1_petrified_cultist"; Description = "尖塔残局第 1 关：从 13 张候选牌选择 7 张，玩家 8 HP，击败 54 生命石化狂信徒；不限回合，除非一方死亡。" },
  @{ Stage = 2; Title = $leaderboardTitle2; PuzzleId = "difficulty2_armor_threshold"; Description = "尖塔残局第 2 关：从 14 张候选牌选择 6 张，并从火焰、易伤、虚弱药水中选择 1 瓶，处理保留格挡门槛。" },
  @{ Stage = 3; Title = "尖塔残局 03：双奇巧铃鸣与伪刃破绽"; PuzzleId = "difficulty3_cunning_bell"; Description = "尖塔残局第 3 关：选择 8 张牌、1 瓶药水、1 个遗物，每回合抽 6 张牌，围绕奇巧弃牌、铃鸣两牌上限和连环破绽击败敌人。" },
  @{ Stage = 4; Title = "尖塔残局 04：灼伤洗牌与消耗窗口"; PuzzleId = "difficulty4_burn_countdown"; Description = "尖塔残局第 4 关：选择 9 张牌和 1 瓶药水，在第 5 回合结束前利用灼伤洗牌、状态牌收益和消耗窗口击败敌人。" },
  @{ Stage = 5; Title = "尖塔残局 05：人工制品虚空锁"; PuzzleId = "difficulty5_void_lock"; Description = "尖塔残局第 5 关：选择 10 张牌、1 瓶药水、1 个遗物，处理人工制品、虚空、药水相位和虚空坍缩。" },
  @{ Stage = 6; Title = "尖塔残局 06：平静破绽与怒火越界"; PuzzleId = "difficulty6_stance_breach"; Description = "尖塔残局第 6 关：选择 10 张牌、1 瓶药水、1 个遗物，利用姿态、平静返能和愤怒双刃完成斩杀。" },
  @{ Stage = 7; Title = "尖塔残局 07：解毒腺与毒雾连锁"; PuzzleId = "difficulty7_poison_antidote"; Description = "尖塔残局第 7 关：选择 11 张牌、1 瓶药水、1 个遗物，围绕毒、催化、毒雾、解毒腺和毒转格挡通关。" },
  @{ Stage = 8; Title = "尖塔残局 08：绝缘破裂与黑球过载"; PuzzleId = "difficulty8_orb_overload"; Description = "尖塔残局第 8 关：选择 12 张牌、1 瓶药水、1 个遗物，管理充能球、黑暗储值、冰霜绝缘破裂、集中与循环。" },
  @{ Stage = 9; Title = "尖塔残局 09：破镜神格与护甲反射"; PuzzleId = "difficulty9_divinity_mirror"; Description = "尖塔残局第 9 关：选择 13 张镜像牌、1 瓶药水、1 个遗物，每回合镜像抽取 4 张，并在回合开始稳定获得真言，利用神格和护甲反射击败敌人。" },
  @{ Stage = 10; Title = "尖塔残局 10：时间裂隙与三相斩杀"; PuzzleId = "difficulty10_time_rift"; Description = "尖塔残局第 10 关：选择 16 张裂隙牌、1 瓶药水、1 个遗物，处理相位门槛、充能、标记、回声、延迟伤害与过热。" }
)

$games = Get-ApiData (Invoke-GongdouApi GET "/api/admin/games")
$game = @($games | Where-Object { $_.executableName -eq $executableName -or $_.name -eq $gameName } | Select-Object -First 1)[0]
if ($null -eq $game) {
  $gameId = Get-CreateId (Invoke-GongdouApi POST "/api/admin/games" @{
    name = $gameName
    iconUrl = ""
    coverUrl = ""
    gameEngine = "Other"
    executableName = $executableName
    modDownloadUrl = ""
  })
} else {
  $gameId = [int]$game.id
}

$categories = Get-ApiData (Invoke-GongdouApi GET "/api/admin/channel-categories")
$category = @($categories | Where-Object { $_.name -eq $categoryName } | Select-Object -First 1)[0]
if ($null -eq $category) {
  $categoryId = Get-CreateId (Invoke-GongdouApi POST "/api/admin/channel-categories" @{
    name = $categoryName
    sortOrder = 100
  })
} else {
  $categoryId = [int]$category.id
}

$channelsPage = Get-ApiData (Invoke-GongdouApi GET "/api/admin/channels?page=1&pageSize=100")
$channels = Get-PagedItems $channelsPage
$channel = @($channels | Where-Object { $_.name -eq $channelName } | Select-Object -First 1)[0]
if ($null -eq $channel) {
  $channelId = Get-CreateId (Invoke-GongdouApi POST "/api/admin/channels" @{
    name = $channelName
    description = "杀戮尖塔2频道"
    iconUrl = $null
    coverUrl = $null
    categoryId = $categoryId
    gameId = $gameId
    lobbyName = "大厅"
    plazaName = "广场"
    enableChat = $true
    enableLeaderboard = $true
    sortOrder = 100
  })
} else {
  $channelId = [int]$channel.id
}

$challengeConfig = @{
  puzzleSetId = "sts2_puzzle_series_01"
  puzzleSetName = "尖塔残局"
  stageCount = 10
  stageIndex = 1
  rulesVersion = 14
  puzzleId = "difficulty1_petrified_cultist"
  puzzleName = "石化狂信徒的第一课"
  puzzleDoc = "docs/D1-D10_final_puzzles.md"
  gameVersion = "v0.103.2+"
  winCondition = "kill_enemy_no_turn_limit"
  loadoutSource = "in_game"
  ranking = "completedStages_then_turns"
  player = @{
    character = "Ironclad"
    ascensionLevel = 0
    startingHp = 8
    maxHp = 8
    maxEnergy = 3
    drawPerTurn = 5
  }
  enemy = @{
    id = "CalcifiedCultist"
    name = "石化狂信徒"
    baseHp = 54
    actionPattern = @(
      @{ turn = 1; action = "attack"; amount = 6 },
      @{ turn = 2; action = "attack"; amount = 12 },
      @{ turn = 3; action = "attack"; amount = 15 },
      @{ turn = 4; action = "attack"; amount = 18 }
    )
  }
  minCards = 7
  maxCards = 7
  maxPotions = 0
  maxRelics = 0
  scoreRule = @{
    formula = "completedStages * 50000000 - cumulativeTurns"
    round = "floor"
    vars = @{
      completedStages = '$.completedStages'
      cumulativeTurns = '$.cumulativeTurns'
      stageCount = '$.stageCount'
    }
    minTimeMs = 1000
    maxTimeMs = 3600000
    minEventCount = 1
    maxEventCount = 1000
    requireEvidence = $true
  }
  stages = @($stageLeaderboards | ForEach-Object {
    @{
      stageIndex = [int]$_.Stage
      puzzleId = $_.PuzzleId
      title = $_.Title
      description = $_.Description
    }
  })
}

function New-Resource($id, $name, $description = "", $maxCopies = 1) {
  return @{
    id = $id
    name = $name
    description = $description
    weight = 1
    maxCopies = $maxCopies
  }
}

$resources = @{
  cardPool = @(
    New-Resource "StrikeIronclad" "打击" "造成6点伤害。" 4
    New-Resource "DefendIronclad" "防御" "获得5点格挡。" 3
    New-Resource "Bash" "痛击" "造成8点伤害。`n给予2层易伤。" 1
    New-Resource "PerfectedStrike" "完美打击" "造成6点伤害。`n你每有一张名字中含有`“打击`”的牌，伤害+2。" 1
    New-Resource "D1_HeavyHammer" "重锤（改）" "3 费，造成 23 点伤害。" 1
    New-Resource "BodySlam" "全身撞击" "造成你当前格挡值的伤害。" 1
    New-Resource "IronWave" "铁斩波" "获得5点格挡。`n造成5点伤害。" 1
    New-Resource "FlyingSword" "飞剑回旋镖" "随机对敌人造成3点伤害3次。" 1
  )
  potionPool = @()
  relicPool = @()
}

$editorSchema = @{
  loadoutSource = "in_game"
  note = "STS2 残局资源选择由 MOD 内游戏 UI 负责；D1-D10 均以内置 MOD 规则和 docs/D1-D10_final_puzzles.md 为准。"
}

$presets = Get-ApiData (Invoke-GongdouApi GET "/api/admin/channels/$channelId/presets")
$preset = @($presets | Where-Object {
  $_.name -eq $presetName `
    -or $_.name -eq "尖塔残局：破损训练偶" `
    -or ($_.challengeType -eq "slay_the_spire_2_puzzle" -and $_.scoreField -eq "timeMs")
} | Select-Object -First 1)[0]
$presetPayload = @{
  name = $presetName
  description = "杀戮尖塔2尖塔残局系列，共 10 关；不限回合，除非一方死亡；通过第 1 关即可入榜，关数优先，同关数按累计回合数排序。"
  challengeType = "slay_the_spire_2_puzzle"
  sortType = 0
  valueFormat = 0
  scoreField = "progressScore"
  scoreLabel = "通关关数/回合"
  configJson = $challengeConfig
  resourcesJson = $resources
  editorSchemaJson = $editorSchema
  isActive = $true
}
if ($null -eq $preset) {
  $presetId = Get-CreateId (Invoke-GongdouApi POST "/api/admin/channels/$channelId/presets" $presetPayload)
} else {
  $presetId = [int]$preset.id
  Invoke-GongdouApi PUT "/api/admin/channels/$channelId/presets/$presetId" $presetPayload | Out-Null
}

$leaderboards = Get-ApiData (Invoke-GongdouApi GET "/api/admin/channels/$channelId/leaderboards")
$leaderboard = @($leaderboards | Where-Object {
  $_.title -eq $leaderboardTitle `
    -or $_.title -eq $stage1Title `
    -or $_.title -eq "破损训练偶" `
    -or $_.title -eq "尖塔残局 01：破损训练偶" `
    -or $_.title -eq "尖塔残局 01：裂纹哨卫" `
    -or $_.title -eq "尖塔残局 01：巨斧窗口" `
    -or $_.title -eq "尖塔残局 01：巨斧重校" `
    -or $_.title -eq "尖塔残局 01：钙化邪教徒的第一课" `
    -or ($_.presetId -eq $presetId -and $_.configOverride.stageIndex -eq 1)
} | Select-Object -First 1)[0]
$leaderboardPayload = @{
  title = $leaderboardTitle
  description = "尖塔残局十关连战：一个预设、一个排行榜，客户端结算浮层按第 1-10 关连续推进；不限回合，除非一方死亡；通过第 1 关即可入榜，通关关数优先，同关数按累计回合数排序。"
  coverUrl = $null
  presetId = $presetId
  startTime = (Get-Date).ToUniversalTime().ToString("o")
  endTime = (Get-Date).ToUniversalTime().AddYears(5).ToString("o")
  isActive = $true
  configOverride = @{
    stageIndex = 1
    puzzleId = "difficulty1_petrified_cultist"
    mode = "single_leaderboard_stage_chain"
    stageCount = 10
  }
}
if ($null -eq $leaderboard) {
  $leaderboardId = Get-CreateId (Invoke-GongdouApi POST "/api/admin/channels/$channelId/leaderboards" $leaderboardPayload)
} else {
  $leaderboardId = [int]$leaderboard.id
  Invoke-GongdouApi PUT "/api/admin/channels/$channelId/leaderboards/$leaderboardId" $leaderboardPayload | Out-Null
}

$leaderboards = Get-ApiData (Invoke-GongdouApi GET "/api/admin/channels/$channelId/leaderboards")
$inactiveLeaderboardIds = @()
foreach ($old in @($leaderboards | Where-Object {
  [int]$_.id -ne $leaderboardId `
    -and $_.presetId -eq $presetId `
    -and ($_.title -like "尖塔残局 *" -or $_.title -eq $leaderboardTitle2)
})) {
  $oldStartTime = if ($old.startTime) { $old.startTime } else { (Get-Date).ToUniversalTime().ToString("o") }
  $oldEndTime = if ($old.endTime) { $old.endTime } else { (Get-Date).ToUniversalTime().AddYears(5).ToString("o") }
  $oldConfigOverride = if ($old.configOverride) { $old.configOverride } else { @{} }
  $inactivePayload = @{
    title = $old.title
    description = $old.description
    coverUrl = $old.coverUrl
    presetId = $presetId
    startTime = $oldStartTime
    endTime = $oldEndTime
    isActive = $false
    configOverride = $oldConfigOverride
  }
  Invoke-GongdouApi PUT "/api/admin/channels/$channelId/leaderboards/$([int]$old.id)" $inactivePayload | Out-Null
  $inactiveLeaderboardIds += [int]$old.id
}

$modUploadResult = $null
if (-not [string]::IsNullOrWhiteSpace($ModZipPath)) {
  Test-ServerDiskForPublish
  $modUploadResult = Invoke-ModUpload -GameId $gameId -Path $ModZipPath
  if (-not $SkipServerModCleanup) {
    $cleanupScript = Join-Path $PSScriptRoot "cleanup-server-mod-packages.ps1"
    if ($SkipServerModCleanupTimer) {
      & $cleanupScript -Keep 2 -RunNow | Out-Host
    }
    else {
      & $cleanupScript -Keep 2 -InstallTimer -RunNow | Out-Host
    }
  }
  Test-ServerDiskForPublish
}

[pscustomobject]@{
  gameId = $gameId
  channelId = $channelId
  presetId = $presetId
  leaderboardId = $leaderboardId
  inactiveLeaderboardIds = $inactiveLeaderboardIds
  executableName = $executableName
  modUpload = $modUploadResult
}
