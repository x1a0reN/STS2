#include <array>
#include <cmath>
#include <fstream>
#include <iostream>
#include <sstream>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>
#include <algorithm>
#include <functional>
#include <cstdlib>

const int N = 12;
using Counts = std::array<unsigned char, N>;
enum Card { BASH, PREPARED, SURVIVOR, FINISHER, BACKSTAB, DAGGER, QUICK, NEUTRALIZE, STRIKE, DEFEND, SHADOW_STEP, FEINT };
enum Potion { SLY_BREW, VULN_POTION, FIRE_POTION };
enum Relic { SHARP_DICE, RETURN_HOLSTER, HOLLOW_AMULET };

const int POOL[N] = {1,1,1,1,2,1,2,1,3,2,2,2};
const int COST[N] = {2,0,1,1,1,1,1,0,1,1,1,0};
const char* CN[N] = {"重击","预备（改）","生存者（改）","终结（改）","背刺（改，狡黠）","匕首投掷（改，非狡黠）","佯刺（改，非狡黠）","中和","打击","防御","影步（改，狡黠）","虚刃（改，非狡黠）"};
const char* POTION_CN[3] = {"狡黠药水","破甲药水","火焰药水"};
const char* RELIC_CN[3] = {"锋利骰子","折返皮套","空心护符"};

struct Vec { double p[5]{}; };
struct Draw { Counts hand{}, rest{}, discard{}; double prob{}; };
struct Ctx {
  Counts hand{}, draw{}, discard{};
  int energy=3, block=0, enemy=0, armor=0, vuln=0, weak=0;
  int potion_used=0, sly=0, first_discard=0, sharp_used=0, played=0;
  int active_backstab=0, bad_discard=0;
};
struct CtxKey {
  Counts hand{}, draw{}, discard{};
  int energy=0, block=0, enemy=0, armor=0, vuln=0, weak=0, potion_used=0, sly_used=0, active_backstab=0, bad_discard=0;
  bool operator==(CtxKey const& o) const {
    return energy==o.energy&&block==o.block&&enemy==o.enemy&&armor==o.armor&&vuln==o.vuln&&weak==o.weak&&potion_used==o.potion_used&&sly_used==o.sly_used&&active_backstab==o.active_backstab&&bad_discard==o.bad_discard&&hand==o.hand&&draw==o.draw&&discard==o.discard;
  }
};
struct CtxKeyHash {
  size_t operator()(CtxKey const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.energy); mix(k.block); mix(k.enemy); mix(k.armor); mix(k.vuln); mix(k.weak); mix(k.potion_used); mix(k.sly_used); mix(k.active_backstab); mix(k.bad_discard);
    for(auto v:k.hand) mix(v); for(auto v:k.draw) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct Key {
  int turn,hp,enemy,armor,vuln,weak,potion_used,potion,relic;
  Counts hand,draw,discard;
  bool operator==(Key const& o) const {
    return turn==o.turn&&hp==o.hp&&enemy==o.enemy&&armor==o.armor&&vuln==o.vuln&&weak==o.weak&&potion_used==o.potion_used&&potion==o.potion&&relic==o.relic&&hand==o.hand&&draw==o.draw&&discard==o.discard;
  }
};
struct KeyHash {
  size_t operator()(Key const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.turn); mix(k.hp); mix(k.enemy); mix(k.armor); mix(k.vuln); mix(k.weak); mix(k.potion_used); mix(k.potion); mix(k.relic);
    for(auto v:k.hand) mix(v); for(auto v:k.draw) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};

static std::unordered_map<Key, Vec, KeyHash> memo;
static long long solve_hits=0, solve_misses=0;
static int START_HP=22;
static int ACTIVE_CAP=2;

int total(Counts const& c){ int s=0; for(auto v:c) s+=v; return s; }
Counts add(Counts a, Counts const& b){ for(int i=0;i<N;i++) a[i]+=b[i]; return a; }
Counts sub_one(Counts a, int card){ a[card]--; return a; }
Counts add_one(Counts a, int card){ a[card]++; return a; }
int atk(int base,int vuln){ return vuln>0 ? (base*3)/2 : base; }
int weakd(int base,int weak){ return weak>0 ? (base*3)/4 : base; }
void deal(int &enemy,int &armor,int amount){ int b=std::min(armor,amount); armor-=b; enemy-=amount-b; }
bool is_sly(int card){ return card==BACKSTAB || card==SHADOW_STEP; }
Vec fail(){ Vec v; v.p[4]=1; return v; }
Vec kill(int turn){ Vec v; v.p[turn-1]=1; return v; }
double success(Vec const& v){ return v.p[0]+v.p[1]+v.p[2]+v.p[3]; }
bool better(Vec const& a, Vec const& b, bool has){ if(!has) return true; if(std::abs(success(a)-success(b))>1e-12) return success(a)>success(b); for(int i=0;i<4;i++) if(std::abs(a.p[i]-b.p[i])>1e-12) return a.p[i]>b.p[i]; return false; }
Vec merge_vec(std::vector<std::pair<double,Vec>> const& items){ Vec out; for(auto const& it:items) for(int i=0;i<5;i++) out.p[i]+=it.first*it.second.p[i]; return out; }

long long C(int n,int k){ if(k<0||k>n) return 0; if(k==0||k==n) return 1; long long r=1; for(int i=1;i<=k;i++) r=r*(n-k+i)/i; return r; }

std::vector<Draw> draw_outcomes(Counts cards, int n, Counts fixed_discard={}) {
  std::vector<Draw> out; int m=total(cards); if(n>=m){ out.push_back({cards,{},fixed_discard,1.0}); return out; }
  long long denom=C(m,n); Counts pick{};
  std::function<void(int,int,long long)> rec = [&](int i,int left,long long ways){
    if(i==N){ if(left==0){ Counts rest{}; for(int j=0;j<N;j++) rest[j]=cards[j]-pick[j]; out.push_back({pick,rest,fixed_discard,(double)ways/(double)denom}); } return; }
    int mx=std::min<int>(cards[i],left);
    for(int k=0;k<=mx;k++){ pick[i]=k; rec(i+1,left-k,ways*C(cards[i],k)); }
    pick[i]=0;
  };
  rec(0,n,1); return out;
}

std::vector<Draw> next_draws(Counts draw, Counts discard, int n) {
  if(total(draw)>=n) return draw_outcomes(draw,n,discard);
  Counts fixed=draw; int need=n-total(draw);
  if(total(discard)==0) return {{fixed,{}, {},1.0}};
  auto tmp=draw_outcomes(discard,need,{});
  for(auto &d:tmp) d.hand=add(fixed,d.hand);
  return tmp;
}

int loose_ub(int turn, Counts hand, Counts draw, Counts discard, int armor, int potion_used, int potion, int relic) {
  Counts cards=add(add(hand,draw),discard); int turns_left=5-turn; if(turns_left<=0) return 0;
  int dmg[N]={12,0,0,20,9,13,12,4,9,0,7,4};
  std::vector<int> pool; for(int i=0;i<N;i++) for(int k=0;k<cards[i];k++) pool.push_back(dmg[i]);
  std::sort(pool.begin(),pool.end(),std::greater<int>());
  int active=0; for(int i=0;i<(int)pool.size() && i<ACTIVE_CAP*turns_left;i++) active+=pool[i];
  int sly=std::min<int>(cards[BACKSTAB]+cards[SHADOW_STEP],turns_left) * (relic==SHARP_DICE ? 19 : 15);
  int pot=(!potion_used && potion==FIRE_POTION) ? 18 : 0;
  return active+sly+pot-armor;
}

Ctx discard_card(Ctx ctx,int card,int relic){
  ctx.hand=sub_one(ctx.hand,card);
  if(!ctx.first_discard){ ctx.first_discard=1; if(relic==RETURN_HOLSTER) ctx.energy+=1; else if(relic==HOLLOW_AMULET) ctx.block+=6; }
  if(is_sly(card)){
    int dmg=card==BACKSTAB ? 10 : 7;
    if(card==SHADOW_STEP) ctx.block+=4;
    if(card==BACKSTAB && relic==SHARP_DICE && !ctx.sharp_used && ctx.block==0){ dmg+=2; ctx.sharp_used=1; }
    ctx.sly++;
    deal(ctx.enemy,ctx.armor,atk(dmg,ctx.vuln));
  }
  else ctx.bad_discard=1;
  ctx.discard=add_one(ctx.discard,card); return ctx;
}
bool play_bash(Ctx const& in, Ctx &out){ if(in.hand[BASH]==0||in.energy<2||in.played>=ACTIVE_CAP) return false; out=in; out.hand=sub_one(out.hand,BASH); out.energy-=2; out.played++; deal(out.enemy,out.armor,atk(8,out.vuln)); out.vuln+=2; out.discard=add_one(out.discard,BASH); return true; }
std::vector<Ctx> play_engine(Ctx const& in,int engine,int relic){ std::vector<Ctx> out; if(in.first_discard||in.hand[engine]==0||in.energy<COST[engine]||total(in.hand)<=1||in.played>=ACTIVE_CAP) return out; Ctx base=in; base.hand=sub_one(base.hand,engine); base.energy-=COST[engine]; base.played++; if(engine==SURVIVOR) base.block+=5; for(int c=0;c<N;c++) if(base.hand[c]>0){ Ctx x=discard_card(base,c,relic); x.discard=add_one(x.discard,engine); out.push_back(x); } return out; }

void gen_subsets(Counts hand,int energy,int allow_bash,int max_cards,std::vector<Counts>& out){
  Counts cur{};
  std::function<void(int,int,int)> rec=[&](int i,int spent,int used){
    if(i==N){ out.push_back(cur); return; }
    if(i==PREPARED||i==SURVIVOR||(!allow_bash&&i==BASH)){ rec(i+1,spent,used); return; }
    int mx=std::min<int>(hand[i], max_cards-used);
    for(int k=0;k<=mx;k++){ int ns=spent+k*COST[i]; if(ns<=energy){ cur[i]=k; rec(i+1,ns,used+k); } }
    cur[i]=0;
  }; rec(0,0,0);
}
bool play_subset(Ctx const& in, Counts sub, Ctx &out){
  if(in.played+total(sub)>ACTIVE_CAP) return false; out=in;
  for(int c=0;c<N;c++) for(int k=0;k<sub[c];k++){ if(out.hand[c]==0||out.energy<COST[c]) return false; out.hand=sub_one(out.hand,c); out.energy-=COST[c]; out.played++; switch(c){ case STRIKE: deal(out.enemy,out.armor,atk(6,out.vuln)); break; case DEFEND: out.block+=5; break; case BASH: deal(out.enemy,out.armor,atk(8,out.vuln)); out.vuln+=2; break; case NEUTRALIZE: deal(out.enemy,out.armor,atk(3,out.vuln)); out.weak+=1; break; case QUICK: deal(out.enemy,out.armor,atk(8,out.vuln)); break; case DAGGER: deal(out.enemy,out.armor,atk(9,out.vuln)); break; case FEINT: deal(out.enemy,out.armor,atk(4,out.vuln)); break; case SHADOW_STEP: deal(out.enemy,out.armor,atk(5,out.vuln)); out.block+=3; out.active_backstab++; break; case BACKSTAB: deal(out.enemy,out.armor,atk(6,out.vuln)); out.active_backstab++; break; case FINISHER: deal(out.enemy,out.armor,atk(8+6*out.sly,out.vuln)); break; default: return false; } out.discard=add_one(out.discard,c); }
  return true;
}

std::vector<Ctx> end_contexts(Key const& k){
  Ctx base; base.hand=k.hand; base.draw=k.draw; base.discard=k.discard; base.enemy=k.enemy; base.armor=k.armor; base.vuln=k.vuln; base.weak=k.weak; base.potion_used=k.potion_used;
  std::vector<Ctx> starts{base};
  if(!k.potion_used){ if(k.potion==FIRE_POTION){ Ctx x=base; deal(x.enemy,x.armor,18); x.potion_used=1; starts.push_back(x); } else if(k.potion==VULN_POTION){ Ctx x=base; x.vuln+=2; x.potion_used=1; starts.push_back(x); } else if(k.potion==SLY_BREW){ Ctx b=base; b.potion_used=1; for(int c=0;c<N;c++) if(b.hand[c]>0) starts.push_back(discard_card(b,c,k.relic)); } }
  std::vector<Ctx> finals; int orders[3][2]={{-1,-1},{PREPARED,-1},{SURVIVOR,-1}};
  for(auto s:starts){ std::vector<Ctx> pre{s}; Ctx pb; if(play_bash(s,pb)) pre.push_back(pb); for(auto ctx1:pre){ for(auto &ord:orders){ std::vector<Ctx> states{ctx1}; bool ok=true; for(int oi=0;oi<2&&ord[oi]!=-1;oi++){ std::vector<Ctx> next; for(auto st:states){ auto tmp=play_engine(st,ord[oi],k.relic); next.insert(next.end(),tmp.begin(),tmp.end()); } if(next.empty()){ok=false; break;} states.swap(next); } if(!ok) continue; for(auto ctx2:states){ std::vector<Ctx> post{ctx2}; Ctx po; if(ctx2.hand[BASH]>0 && play_bash(ctx2,po)) post.push_back(po); for(auto ctx3:post){ std::vector<Counts> subs; gen_subsets(ctx3.hand,ctx3.energy,ctx3.hand[BASH]>0,ACTIVE_CAP-ctx3.played,subs); for(auto sub:subs){ Ctx end; if(play_subset(ctx3,sub,end)) finals.push_back(end); } } } } } }
  std::vector<Ctx> uniq;
  std::unordered_set<CtxKey, CtxKeyHash> seen;
  for(auto const& c:finals){
    CtxKey ck{c.hand,c.draw,c.discard,c.energy,c.block,c.enemy,c.armor,c.vuln,c.weak,c.potion_used,c.sly>0,c.active_backstab,c.bad_discard};
    if(seen.insert(ck).second) uniq.push_back(c);
  }
  return uniq;
}

Vec solve(Key k){
  if(k.enemy > loose_ub(k.turn,k.hand,k.draw,k.discard,k.armor,k.potion_used,k.potion,k.relic)) return fail();
  auto it=memo.find(k); if(it!=memo.end()){ solve_hits++; return it->second; } solve_misses++;
  Vec best; bool has=false;
  for(auto ctx:end_contexts(k)){
    Vec cand;
    if(ctx.enemy<=0) cand=kill(k.turn);
    else if(k.turn>=4) cand=fail();
    else{
      int incoming=0, armor_gain=0; if(k.turn==1){ incoming=weakd(8,ctx.weak); if(ctx.sly==0) armor_gain=14; } else if(k.turn==2){ incoming=weakd(22,ctx.weak); if(ctx.sly==0) armor_gain=20; } else if(k.turn==3){ incoming=weakd(22,ctx.weak); if(ctx.sly==0) armor_gain=8; }
      armor_gain += 8 * ctx.active_backstab;
      if(ctx.bad_discard) armor_gain += 6;
      int hp2=k.hp-std::max(0,incoming-ctx.block);
      if(hp2<=0) cand=fail(); else { Counts discard2=add(ctx.discard,ctx.hand); std::vector<std::pair<double,Vec>> items; for(auto d:next_draws(ctx.draw,discard2,5)){ Key nk{k.turn+1,hp2,ctx.enemy,ctx.armor+armor_gain,std::max(0,ctx.vuln-1),std::max(0,ctx.weak-1),ctx.potion_used,k.potion,k.relic,d.hand,d.rest,d.discard}; items.push_back({d.prob,solve(nk)}); } cand=merge_vec(items); }
    }
    if(better(cand,best,has)){ best=cand; has=true; }
  }
  if(!has) best=fail(); memo.emplace(k,best); return best;
}

Vec result_for(Counts deck,int potion,int relic,int enemy_hp){
  std::vector<std::pair<double,Vec>> items; for(auto d:draw_outcomes(deck,5)){ Key k{1,START_HP,enemy_hp,0,0,0,0,potion,relic,d.hand,d.rest,{}}; items.push_back({d.prob,solve(k)}); } return merge_vec(items);
}

void gen_decks_rec(int i,int left,Counts& cur,std::vector<Counts>& out){ if(i==N){ if(left==0 && cur[BACKSTAB]==2 && cur[FINISHER]==1 && cur[SHADOW_STEP]>=1 && (cur[PREPARED]+cur[SURVIVOR]==1)) out.push_back(cur); return; } for(int k=0;k<=std::min(POOL[i],left);k++){ cur[i]=k; gen_decks_rec(i+1,left-k,cur,out); } cur[i]=0; }
std::vector<Counts> decks(){ std::vector<Counts> out; Counts c{}; gen_decks_rec(0,8,c,out); return out; }
std::string deck_str(Counts d){ std::ostringstream os; bool first=true; for(int i=0;i<N;i++) if(d[i]){ if(!first) os<<"、"; first=false; os<<CN[i]; if(d[i]>1) os<<" x"<<(int)d[i]; } return os.str(); }
int first_turn(Vec v){ for(int i=0;i<4;i++) if(v.p[i]>1e-9) return i+1; return 0; }
std::string family(Counts d,int p,int r){ if(d[BACKSTAB]>=2&&d[SHADOW_STEP]>=1&&p==SLY_BREW&&r==SHARP_DICE) return "药水骰子双狡黠快线"; if(d[BACKSTAB]>=2&&d[SHADOW_STEP]>=1&&r==SHARP_DICE) return "锋利骰子双狡黠爆发线"; if(d[FINISHER]&&(d[PREPARED]||d[SURVIVOR])&&d[SHADOW_STEP]>=1) return "双狡黠终结线"; if(d[FEINT]||d[DAGGER]||d[QUICK]) return "伪狡黠干扰线"; if(d[SURVIVOR]&&r==HOLLOW_AMULET) return "弃牌格挡稳线"; if(d[BASH]&&p==VULN_POTION) return "双易伤攻击线"; if(p==FIRE_POTION) return "火焰补刀线"; return "混合线"; }

struct Row{ Counts deck; int potion,relic,ft; Vec vec; double succ; std::string fam,display; };
int main(int argc, char** argv){
  if(argc>=15 && std::string(argv[1])=="build"){
    int enemy_hp=std::atoi(argv[2]);
    int potion=std::atoi(argv[3]);
    int relic=std::atoi(argv[4]);
    if(argc>=18) START_HP=std::atoi(argv[17]);
    if(argc>=19) ACTIVE_CAP=std::atoi(argv[18]);
    Counts d{};
    for(int i=0;i<N;i++) d[i]=(unsigned char)std::atoi(argv[5+i]);
    Vec v=result_for(d,potion,relic,enemy_hp);
    std::cout<<"hp "<<enemy_hp<<" success "<<success(v)*100<<" first "<<first_turn(v)<<" vec ["<<v.p[0]*100<<","<<v.p[1]*100<<","<<v.p[2]*100<<","<<v.p[3]*100<<","<<v.p[4]*100<<"] "<<family(d,potion,relic)<<" "<<deck_str(d)<<"；"<<POTION_CN[potion]<<"；"<<RELIC_CN[relic]<<"\n";
    return 0;
  }
  int enemy_hp=78;
  if(argc>=2) enemy_hp=std::atoi(argv[1]);
  if(argc>=4) START_HP=std::atoi(argv[3]);
  if(argc>=5) ACTIVE_CAP=std::atoi(argv[4]);
  bool quiet = argc>=3 && std::string(argv[2])=="quiet";
  int max_builds = argc>=6 ? std::atoi(argv[5]) : 0;
  auto ds=decks(); std::vector<Row> rows; int done=0,total=(int)ds.size()*9;
  for(auto d:ds) for(int p=0;p<3;p++) for(int r=0;r<3;r++){ memo.clear(); Vec v=result_for(d,p,r,enemy_hp); Row row{d,p,r,first_turn(v),v,success(v),family(d,p,r),deck_str(d)+"；"+POTION_CN[p]+"；"+RELIC_CN[r]}; rows.push_back(row); done++; if(!quiet && (done%25==0||done==total)){ std::cout<<"audited "<<done<<"/"<<total<<"\n"; std::cout.flush(); } if(max_builds && done>=max_builds) goto done_loop; }
done_loop:
  std::sort(rows.begin(),rows.end(),[](auto&a,auto&b){ if(std::abs(a.succ-b.succ)>1e-12) return a.succ>b.succ; int af=a.ft?a.ft:99,bf=b.ft?b.ft:99; if(af!=bf) return af<bf; return a.vec.p[1]>b.vec.p[1]; });
  int perfect=0; for(auto&r:rows) if(r.succ>0.999999) perfect++;
  Row best_turn[5]; bool has_turn[5]{};
  std::vector<std::string> fam_names = {"药水骰子双狡黠快线","锋利骰子双狡黠爆发线","双狡黠终结线","伪狡黠干扰线","弃牌格挡稳线","双易伤攻击线","火焰补刀线","混合线"};
  std::vector<Row> best_fam(fam_names.size()); std::vector<bool> has_fam(fam_names.size(), false);
  for(auto const& r:rows){
    int t=r.ft;
    if(t>=0&&t<=4&&(!has_turn[t]||r.succ>best_turn[t].succ)){ best_turn[t]=r; has_turn[t]=true; }
    for(int i=0;i<(int)fam_names.size();i++) if(r.fam==fam_names[i]&&(!has_fam[i]||r.succ>best_fam[i].succ)){ best_fam[i]=r; has_fam[i]=true; }
  }
  std::ofstream jf("difficulty3_sly_fast_audit.json");
  jf<<"{\"enemy_hp\":"<<enemy_hp<<",\"player_hp\":"<<START_HP<<",\"active_play_cap\":"<<ACTIVE_CAP<<",\"legal_deck_count\":"<<ds.size()<<",\"legal_build_count\":"<<rows.size()<<",\"perfect_success_count\":"<<perfect<<",\"top30\":[";
  for(int i=0;i<30&&i<(int)rows.size();i++){ if(i) jf<<","; auto&r=rows[i]; jf<<"{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"]}"; }
  jf<<"],\"best_by_first_turn\":{";
  bool first_json=true; for(int t=0;t<=4;t++) if(has_turn[t]){ if(!first_json) jf<<","; first_json=false; auto&r=best_turn[t]; jf<<"\""<<t<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"]}"; }
  jf<<"},\"best_by_family\":{";
  first_json=true; for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ if(!first_json) jf<<","; first_json=false; auto&r=best_fam[i]; jf<<"\""<<fam_names[i]<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"]}"; }
  jf<<"},\"cache\":{\"solve_hits\":"<<solve_hits<<",\"solve_misses\":"<<solve_misses<<"}}"; jf.close();
  if(quiet){
    auto&r=rows.front();
    std::cout<<"hp "<<enemy_hp<<" perfect "<<perfect<<" best "<<r.succ*100<<" first "<<r.ft<<" vec ["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"] "<<r.fam<<" "<<r.display<<"\n";
    return 0;
  }
  std::cout<<"enemy_hp "<<enemy_hp<<"\nlegal_deck_count "<<ds.size()<<"\nlegal_build_count "<<rows.size()<<"\nperfect_success_count "<<perfect<<"\ntop30\n";
  for(int i=0;i<30&&i<(int)rows.size();i++){ auto&r=rows[i]; std::cout<<r.succ*100<<" first "<<r.ft<<" "<<r.fam<<" ["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"] "<<r.display<<"\n"; }
  std::cout<<"best_by_first_turn\n";
  for(int t=0;t<=4;t++) if(has_turn[t]){ auto&r=best_turn[t]; std::cout<<t<<" "<<r.succ*100<<" "<<r.fam<<" ["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"] "<<r.display<<"\n"; }
  std::cout<<"best_by_family\n";
  for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ auto&r=best_fam[i]; std::cout<<fam_names[i]<<" "<<r.succ*100<<" first "<<r.ft<<" ["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"] "<<r.display<<"\n"; }
  std::cout<<"cache "<<solve_hits<<" "<<solve_misses<<"\n";
}
