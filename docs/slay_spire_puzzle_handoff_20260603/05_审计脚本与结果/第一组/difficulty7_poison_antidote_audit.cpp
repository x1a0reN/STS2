#include <array>
#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <fstream>
#include <functional>
#include <iostream>
#include <sstream>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

using Counts = std::array<unsigned char, 14>;
enum Card { DEADLY, BOUNCING, POISON_STAB, CATALYST, FUMES, BANE, PREDATOR, DAGGER, LEG_SWEEP, CLOAK, STRIKE, DEFEND, BACKFLIP, CALTROPS };
enum Potion { POISON_POTION, CATALYST_POTION, GHOST_POTION };
enum Relic { SNECKO_SKULL, TOXIC_FUNNEL, NEEDLE_RING };

const int POOL[14] = {2,1,2,1,1,2,1,1,1,1,3,3,1,1};
const int COST[14] = {1,2,1,1,1,1,2,1,2,1,1,1,1,1};
const char* CN[14] = {"致命毒药","弹跳药瓶（改）","带毒刺击（改）","催化剂（改）","毒雾（改）","灾祸（改）","猎杀者（改）","投掷匕首（改）","扫腿","斗篷与匕首（改）","打击","防御","后空翻（改）","铁蒺藜（改）"};
const char* POTION_CN[3] = {"毒药药水","催化药水","幽灵药水"};
const char* RELIC_CN[3] = {"蛇颅","毒液漏斗","针戒"};

int ENEMY_HP=122, PLAYER_HP=32;
int DMG[5]={11,20,28,36,50};
int ARMOR_GAIN[5]={0,14,0,22,0};
int ANTIDOTE[5]={0,6,0,9,0};

struct Vec { double p[6]{}; };
struct Draw { Counts hand{}, rest{}, discard{}; double prob{}; };
struct Key {
  int turn,hp,enemy,armor,poison,weak,potion_used,potion,relic,fumes,caltrops;
  Counts hand,draw,discard;
  bool operator==(Key const& o) const {
    return turn==o.turn&&hp==o.hp&&enemy==o.enemy&&armor==o.armor&&poison==o.poison&&weak==o.weak&&potion_used==o.potion_used&&potion==o.potion&&relic==o.relic&&fumes==o.fumes&&caltrops==o.caltrops&&hand==o.hand&&draw==o.draw&&discard==o.discard;
  }
};
struct KeyHash {
  size_t operator()(Key const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.turn); mix(k.hp); mix(k.enemy); mix(k.armor); mix(k.poison); mix(k.weak); mix(k.potion_used); mix(k.potion); mix(k.relic); mix(k.fumes); mix(k.caltrops);
    for(auto v:k.hand) mix(v); for(auto v:k.draw) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct Ctx {
  Counts hand{}, discard{};
  int enemy=0, armor=0, poison=0, weak=0, potion_used=0, fumes=0, caltrops=0;
  int energy=3, block=0, plays=0, needle_used=0, ghost=0;
};
struct CtxKey {
  Counts hand{}, discard{};
  int enemy=0,armor=0,poison=0,weak=0,potion_used=0,fumes=0,caltrops=0,energy=0,block=0,plays=0,needle_used=0,ghost=0;
  bool operator==(CtxKey const& o) const {
    return enemy==o.enemy&&armor==o.armor&&poison==o.poison&&weak==o.weak&&potion_used==o.potion_used&&fumes==o.fumes&&caltrops==o.caltrops&&energy==o.energy&&block==o.block&&plays==o.plays&&needle_used==o.needle_used&&ghost==o.ghost&&hand==o.hand&&discard==o.discard;
  }
};
struct CtxKeyHash {
  size_t operator()(CtxKey const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.enemy); mix(k.armor); mix(k.poison); mix(k.weak); mix(k.potion_used); mix(k.fumes); mix(k.caltrops); mix(k.energy); mix(k.block); mix(k.plays); mix(k.needle_used); mix(k.ghost);
    for(auto v:k.hand) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct Row { Counts deck; int potion,relic,ft; Vec vec; double succ; std::string fam, display; };
static std::unordered_map<Key, Vec, KeyHash> memo;
Vec solve(Key k);

int total(Counts const& c){ int s=0; for(auto v:c) s+=v; return s; }
Counts add(Counts a, Counts const& b){ for(int i=0;i<14;i++) a[i]+=b[i]; return a; }
Counts sub_one(Counts a,int c){ a[c]--; return a; }
Counts add_one(Counts a,int c){ a[c]++; return a; }
long long C(int n,int k){ if(k<0||k>n) return 0; if(k==0||k==n) return 1; long long r=1; for(int i=1;i<=k;i++) r=r*(n-k+i)/i; return r; }
double success(Vec const& v){ return v.p[0]+v.p[1]+v.p[2]+v.p[3]+v.p[4]; }
Vec fail(){ Vec v; v.p[5]=1; return v; }
Vec kill(int turn){ Vec v; v.p[turn-1]=1; return v; }
bool better(Vec const& a,Vec const& b,bool has){ if(!has) return true; if(std::abs(success(a)-success(b))>1e-12) return success(a)>success(b); for(int i=0;i<5;i++) if(std::abs(a.p[i]-b.p[i])>1e-12) return a.p[i]>b.p[i]; return false; }
Vec merge_vec(std::vector<std::pair<double,Vec>> const& items){ Vec out; for(auto const& it:items) for(int i=0;i<6;i++) out.p[i]+=it.first*it.second.p[i]; return out; }
int weakd(int base,int weak){ return weak>0 ? (base*3)/4 : base; }

void deal(int &enemy,int &armor,int amount){ int b=std::min(armor,amount); armor-=b; enemy-=amount-b; }
void apply_poison(Ctx &c,int amount,int relic){ c.poison += amount + (relic==SNECKO_SKULL ? 1 : 0); if(c.poison>80) c.poison=80; }
void attack(Ctx &c,int base,int relic){
  int dmg=base;
  if(relic==NEEDLE_RING && c.poison>0 && !c.needle_used){ dmg+=6; c.needle_used=1; }
  deal(c.enemy,c.armor,dmg);
}

std::vector<Draw> draw_outcomes(Counts cards,int n,Counts fixed_discard={}) {
  std::vector<Draw> out; int m=total(cards);
  if(n>=m){ out.push_back({cards,{},fixed_discard,1.0}); return out; }
  long long denom=C(m,n); Counts pick{};
  std::function<void(int,int,long long)> rec=[&](int i,int left,long long ways){
    if(i==14){ if(left==0){ Counts rest{}; for(int j=0;j<14;j++) rest[j]=cards[j]-pick[j]; out.push_back({pick,rest,fixed_discard,(double)ways/(double)denom}); } return; }
    int mx=std::min<int>(cards[i],left);
    for(int k=0;k<=mx;k++){ pick[i]=k; rec(i+1,left-k,ways*C(cards[i],k)); }
    pick[i]=0;
  };
  rec(0,n,1); return out;
}

std::vector<Draw> next_draws(Counts draw,Counts discard,int n) {
  if(total(draw)>=n) return draw_outcomes(draw,n,discard);
  Counts fixed=draw; int need=n-total(draw);
  if(total(discard)==0) return {{fixed,{}, {},1.0}};
  auto tmp=draw_outcomes(discard,need,{});
  for(auto &d:tmp) d.hand=add(fixed,d.hand);
  return tmp;
}

bool play_card(Ctx const& in,int card,int relic,Ctx &out) {
  if(in.hand[card]==0 || in.energy<COST[card] || in.plays>=2) return false;
  out=in; out.hand=sub_one(out.hand,card); out.energy-=COST[card]; out.plays++;
  bool exhaust=false;
  switch(card) {
    case DEADLY: apply_poison(out,5,relic); break;
    case BOUNCING: apply_poison(out,8,relic); break;
    case POISON_STAB: attack(out,6,relic); apply_poison(out,3,relic); break;
    case CATALYST: out.poison*=2; if(out.poison>80) out.poison=80; exhaust=true; break;
    case FUMES: out.fumes=1; exhaust=true; break;
    case BANE: attack(out,out.poison>0?14:7,relic); break;
    case PREDATOR: attack(out,15,relic); break;
    case DAGGER: attack(out,9,relic); break;
    case LEG_SWEEP: out.block+=11; out.weak+=2; break;
    case CLOAK: out.block+=6; attack(out,4,relic); break;
    case STRIKE: attack(out,6,relic); break;
    case DEFEND: out.block+=5; break;
    case BACKFLIP: out.block+=7; break;
    case CALTROPS: out.caltrops=1; exhaust=true; break;
  }
  if(!exhaust) out.discard=add_one(out.discard,card);
  return true;
}

bool use_potion(Ctx const& in,int potion,int relic,Ctx &out) {
  if(in.potion_used) return false;
  out=in; out.potion_used=1;
  if(potion==POISON_POTION) apply_poison(out,10,relic);
  else if(potion==CATALYST_POTION){ out.poison*=2; if(out.poison>80) out.poison=80; }
  else if(potion==GHOST_POTION) out.ghost=1;
  return true;
}

std::vector<Ctx> end_contexts(Key const& k) {
  Ctx start; start.hand=k.hand; start.discard=k.discard; start.enemy=k.enemy; start.armor=k.armor; start.poison=k.poison; start.weak=k.weak; start.potion_used=k.potion_used; start.fumes=k.fumes; start.caltrops=k.caltrops;
  std::vector<Ctx> uniq; std::unordered_set<CtxKey, CtxKeyHash> final_seen, action_seen;
  auto push_final=[&](Ctx const& c){
    Ctx normalized=c;
    normalized.discard=add(c.discard,c.hand);
    normalized.hand={};
    normalized.energy=0;
    normalized.plays=0;
    normalized.needle_used=0;
    CtxKey ck{normalized.hand,normalized.discard,normalized.enemy,normalized.armor,normalized.poison,normalized.weak,normalized.potion_used,normalized.fumes,normalized.caltrops,normalized.energy,normalized.block,normalized.plays,normalized.needle_used,normalized.ghost};
    if(final_seen.insert(ck).second) uniq.push_back(normalized);
  };
  std::function<void(Ctx const&)> bestFromCtx = [&](Ctx const& c){
    CtxKey ak{c.hand,c.discard,c.enemy,c.armor,c.poison,c.weak,c.potion_used,c.fumes,c.caltrops,c.energy,c.block,c.plays,c.needle_used,c.ghost};
    if(!action_seen.insert(ak).second) return;
    push_final(c);
    if(c.enemy<=0) return;
    if(!c.potion_used){
      Ctx out;
      if(use_potion(c,k.potion,k.relic,out)) bestFromCtx(out);
    }
    if(c.plays>=2) return;
    for(int card=0;card<14;card++){
      Ctx out;
      if(play_card(c,card,k.relic,out)) bestFromCtx(out);
    }
  };
  bestFromCtx(start);
  return uniq;
}

Ctx start_turn_context(Key const& k) {
  Ctx start;
  start.hand=k.hand;
  start.discard=k.discard;
  start.enemy=k.enemy;
  start.armor=k.armor;
  start.poison=k.poison;
  start.weak=k.weak;
  start.potion_used=k.potion_used;
  start.fumes=k.fumes;
  start.caltrops=k.caltrops;
  return start;
}

Vec end_turn_value(Ctx const& c, Key const& k) {
  if(c.enemy<=0) return kill(k.turn);
  int enemy=c.enemy, poison=c.poison, armor=c.armor;
  if(poison>0){
    enemy-=poison;
    poison -= (k.relic==TOXIC_FUNNEL ? 0 : 1);
    if(poison<0) poison=0;
  }
  if(enemy<=0) return kill(k.turn);
  int antidote=ANTIDOTE[k.turn-1];
  if(k.relic==TOXIC_FUNNEL) antidote=std::max(0,antidote-4);
  int removed=std::min(poison,antidote);
  poison-=removed;
  armor += removed*2 + ARMOR_GAIN[k.turn-1];
  int incoming=weakd(DMG[k.turn-1],c.weak);
  if(c.ghost) incoming/=2;
  if(c.caltrops && incoming>0) deal(enemy,armor,6);
  if(enemy<=0) return kill(k.turn);
  int hp2=k.hp-std::max(0,incoming-c.block);
  if(k.turn>=5 || hp2<=0) return fail();

  Counts discard2=add(c.discard,c.hand);
  std::vector<std::pair<double,Vec>> items;
  for(auto d:next_draws(k.draw,discard2,5)) {
    Key nk{k.turn+1,hp2,enemy,armor,poison,std::max(0,c.weak-1),c.potion_used,k.potion,k.relic,c.fumes,c.caltrops,d.hand,d.rest,d.discard};
    items.push_back({d.prob,solve(nk)});
  }
  return merge_vec(items);
}

Vec best_from_ctx(Ctx const& c, Key const& k, std::unordered_map<CtxKey, Vec, CtxKeyHash>& turn_memo) {
  if(c.enemy<=0) return kill(k.turn);
  CtxKey ck{c.hand,c.discard,c.enemy,c.armor,c.poison,c.weak,c.potion_used,c.fumes,c.caltrops,c.energy,c.block,c.plays,c.needle_used,c.ghost};
  auto it=turn_memo.find(ck);
  if(it!=turn_memo.end()) return it->second;

  Vec best=end_turn_value(c,k);
  if(!c.potion_used){
    Ctx out;
    if(use_potion(c,k.potion,k.relic,out)) {
      Vec cand=best_from_ctx(out,k,turn_memo);
      if(better(cand,best,true)) best=cand;
    }
  }
  if(c.plays<2){
    for(int card=0;card<14;card++){
      Ctx out;
      if(play_card(c,card,k.relic,out)) {
        Vec cand=best_from_ctx(out,k,turn_memo);
        if(better(cand,best,true)) best=cand;
      }
    }
  }

  turn_memo.emplace(ck,best);
  return best;
}

Vec solve(Key k) {
  if(k.hp<=0) return fail();
  if(k.enemy<=0) return kill(k.turn);
  if(k.fumes) {
    Ctx tmp; tmp.poison=k.poison;
    apply_poison(tmp,3,k.relic);
    k.poison=tmp.poison;
  }
  auto it=memo.find(k); if(it!=memo.end()) return it->second;
  Vec best; bool has=false;
  for(auto c:end_contexts(k)) {
    Vec cand=end_turn_value(c,k);
    if(better(cand,best,has)){ best=cand; has=true; }
  }
  if(!has) best=fail();
  memo.emplace(k,best); return best;
}

Vec result_for(Counts deck,int potion,int relic) {
  std::vector<std::pair<double,Vec>> items;
  for(auto d:draw_outcomes(deck,5)) {
    Key k{1,PLAYER_HP,ENEMY_HP,0,0,0,0,potion,relic,0,0,d.hand,d.rest,{}};
    items.push_back({d.prob,solve(k)});
  }
  return merge_vec(items);
}

void gen_decks_rec(int i,int left,Counts& cur,std::vector<Counts>& out) {
  if(i==14){
    if(left==11 && cur[DEADLY]>=1 && cur[CATALYST]==1 && cur[BOUNCING]==1 && cur[POISON_STAB]>=1 && cur[FUMES]==1 && cur[LEG_SWEEP]+cur[CLOAK]+cur[BACKFLIP]>=2) out.push_back(cur);
    return;
  }
  for(int k=0;k<=POOL[i] && left+k<=11;k++){ cur[i]=k; gen_decks_rec(i+1,left+k,cur,out); }
  cur[i]=0;
}
std::vector<Counts> decks(){ std::vector<Counts> out; Counts c{}; gen_decks_rec(0,0,c,out); return out; }
int first_turn(Vec v){ for(int i=0;i<5;i++) if(v.p[i]>1e-9) return i+1; return 0; }
std::string deck_str(Counts d){ std::ostringstream os; bool first=true; for(int i=0;i<14;i++) if(d[i]){ if(!first) os<<"、"; first=false; os<<CN[i]; if(d[i]>1) os<<" x"<<(int)d[i]; } return os.str(); }
std::string family(Counts d,int p,int r){
  if((p==CATALYST_POTION || d[CATALYST]) && r==SNECKO_SKULL) return "蛇颅催化快线";
  if(r==TOXIC_FUNNEL && d[FUMES]) return "毒液漏斗持续线";
  if(r==NEEDLE_RING && (d[BANE]||d[PREDATOR])) return "针戒毒击线";
  if(d[CALTROPS]) return "铁蒺藜防反线";
  if(p==GHOST_POTION) return "幽灵拖回合线";
  if(p==POISON_POTION) return "毒药药水线";
  return "混合线";
}

struct PrintRow { Counts deck; int potion,relic,ft; Vec vec; double succ; std::string fam, display; };

int main(int argc,char** argv){
  if(argc>=19 && std::string(argv[1])=="build"){
    ENEMY_HP=std::atoi(argv[2]); PLAYER_HP=std::atoi(argv[3]);
    int potion=std::atoi(argv[4]), relic=std::atoi(argv[5]);
    Counts d{};
    for(int i=0;i<14;i++) d[i]=(unsigned char)std::atoi(argv[6+i]);
    Vec v=result_for(d,potion,relic);
    std::cout<<"enemy_hp "<<ENEMY_HP<<" player_hp "<<PLAYER_HP<<" success "<<success(v)*100<<" first "<<first_turn(v)<<" ["
      <<v.p[0]*100<<","<<v.p[1]*100<<","<<v.p[2]*100<<","<<v.p[3]*100<<","<<v.p[4]*100<<","<<v.p[5]*100<<"] "
      <<family(d,potion,relic)<<" "<<deck_str(d)<<"；"<<POTION_CN[potion]<<"；"<<RELIC_CN[relic]<<"\n";
    return 0;
  }
  if(argc>=3){ ENEMY_HP=std::atoi(argv[1]); PLAYER_HP=std::atoi(argv[2]); }
  auto env_int=[](const char* name,int fallback){ const char* v=std::getenv(name); return v ? std::atoi(v) : fallback; };
  int max_builds = argc>=4 ? std::atoi(argv[3]) : env_int("GONGDOU_AUDIT_MAX_BUILDS",0);
  int skip_builds = argc>=5 ? std::atoi(argv[4]) : env_int("GONGDOU_AUDIT_SKIP_BUILDS",0);
  int progress_every = env_int("GONGDOU_AUDIT_PROGRESS_EVERY",50);
  bool no_write = std::getenv("GONGDOU_AUDIT_NO_WRITE") && std::string(std::getenv("GONGDOU_AUDIT_NO_WRITE"))=="1";
  bool clear_memo_per_build = std::getenv("GONGDOU_AUDIT_CLEAR_MEMO_PER_BUILD") && std::string(std::getenv("GONGDOU_AUDIT_CLEAR_MEMO_PER_BUILD"))=="1";
  auto ds=decks(); std::vector<PrintRow> rows; int done=0,seen=0,total_builds=(int)ds.size()*9;
  for(auto d:ds) for(int p=0;p<3;p++) for(int r=0;r<3;r++){
    if(seen++<skip_builds) continue;
    if(clear_memo_per_build) memo.clear();
    Vec v=result_for(d,p,r);
    rows.push_back({d,p,r,first_turn(v),v,success(v),family(d,p,r),deck_str(d)+"；"+POTION_CN[p]+"；"+RELIC_CN[r]});
    done++;
    if(progress_every>0 && (done%progress_every==0 || seen==total_builds)){
      std::cout<<"audited "<<done<<"/"<<total_builds<<"\n";
      std::cout.flush();
    }
    if(max_builds && done>=max_builds) goto done_loop;
  }
done_loop:
  std::sort(rows.begin(),rows.end(),[](auto&a,auto&b){ if(std::abs(a.succ-b.succ)>1e-12) return a.succ>b.succ; int af=a.ft?a.ft:99,bf=b.ft?b.ft:99; if(af!=bf) return af<bf; return a.vec.p[2]>b.vec.p[2]; });
  int perfect=0; for(auto&r:rows) if(r.succ>0.999999) perfect++;
  PrintRow best_turn[6]; bool has_turn[6]{};
  std::vector<std::string> fam_names={"蛇颅催化快线","毒液漏斗持续线","针戒毒击线","铁蒺藜防反线","幽灵拖回合线","毒药药水线","混合线"};
  std::vector<PrintRow> best_fam(fam_names.size()); std::vector<bool> has_fam(fam_names.size(),false);
  for(auto const& r:rows){
    int t=r.ft; if(t>=0&&t<=5&&(!has_turn[t]||r.succ>best_turn[t].succ)){ best_turn[t]=r; has_turn[t]=true; }
    for(int i=0;i<(int)fam_names.size();i++) if(r.fam==fam_names[i]&&(!has_fam[i]||r.succ>best_fam[i].succ)){ best_fam[i]=r; has_fam[i]=true; }
  }
  std::ostringstream fn;
  fn<<"difficulty7_poison_antidote_audit";
  if(skip_builds||max_builds) fn<<"_part_"<<skip_builds<<"_"<<rows.size();
  fn<<".json";
  if(!no_write){
    std::ofstream jf(fn.str());
    jf<<"{\"enemy_hp\":"<<ENEMY_HP<<",\"player_hp\":"<<PLAYER_HP<<",\"decision_model\":\"full_action_search\",\"legal_deck_count\":"<<ds.size()<<",\"total_build_count\":"<<total_builds<<",\"skip_builds\":"<<skip_builds<<",\"legal_build_count\":"<<rows.size()<<",\"perfect_success_count\":"<<perfect<<",\"top30\":[";
    for(int i=0;i<30&&i<(int)rows.size();i++){ if(i) jf<<","; auto&r=rows[i]; jf<<"{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"]}"; }
    jf<<"],\"best_by_first_turn\":{"; bool first=true; for(int t=0;t<=5;t++) if(has_turn[t]){ if(!first) jf<<","; first=false; auto&r=best_turn[t]; jf<<"\""<<t<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"]}"; }
    jf<<"},\"best_by_family\":{"; first=true; for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ if(!first) jf<<","; first=false; auto&r=best_fam[i]; jf<<"\""<<fam_names[i]<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"]}"; }
    jf<<"}}";
  }
  auto&r=rows.front();
  std::cout<<"enemy_hp "<<ENEMY_HP<<" player_hp "<<PLAYER_HP<<"\nlegal_deck_count "<<ds.size()<<"\nlegal_build_count "<<rows.size()<<"\nperfect_success_count "<<perfect<<"\n";
  std::cout<<"best "<<r.succ*100<<" first "<<r.ft<<" "<<r.fam<<" ["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"] "<<r.display<<"\n";
  std::cout<<"best_by_first_turn\n";
  for(int t=0;t<=5;t++) if(has_turn[t]){ auto&b=best_turn[t]; std::cout<<t<<" "<<b.succ*100<<" "<<b.fam<<" ["<<b.vec.p[0]*100<<","<<b.vec.p[1]*100<<","<<b.vec.p[2]*100<<","<<b.vec.p[3]*100<<","<<b.vec.p[4]*100<<","<<b.vec.p[5]*100<<"] "<<b.display<<"\n"; }
  std::cout<<"best_by_family\n";
  for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ auto&b=best_fam[i]; std::cout<<fam_names[i]<<" "<<b.succ*100<<" first "<<b.ft<<" ["<<b.vec.p[0]*100<<","<<b.vec.p[1]*100<<","<<b.vec.p[2]*100<<","<<b.vec.p[3]*100<<","<<b.vec.p[4]*100<<","<<b.vec.p[5]*100<<"] "<<b.display<<"\n"; }
}
