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

using Counts = std::array<unsigned char, 13>;
enum Card { ERUPTION, VIGILANCE, EMPTY_FIST, EMPTY_BODY, FLURRY, WHEEL, CONSECRATE, CUT, BOWLING, PROTECT, STRIKE, DEFEND, HALT };
enum Potion { WRATH_POTION, CALM_POTION, FIRE_POTION };
enum Relic { LOTUS, WRATH_GUARD, STANCE_MARK };
enum Stance { NORMAL, CALM, WRATH };

const int POOL[13] = {1,1,1,1,2,1,1,2,1,1,3,3,2};
const int COST[13] = {2,2,1,1,0,2,0,1,1,1,1,1,0};
const char* CN[13] = {"喷发","警惕","化拳","化体（改）","追击（改）","回旋踢（改）","供奉","斩破命运（改）","保龄球（改）","护身（改）","打击","防御","停顿（改）"};
const char* POTION_CN[3] = {"怒火药水","平静药水","火焰药水"};
const char* RELIC_CN[3] = {"紫莲花","怒焰护符","姿态刻印"};

int ENEMY_HP=94, PLAYER_HP=29;
int DMG[5]={12,19,26,34,48};
int ARMOR_GAIN[5]={0,14,0,22,0};
int CALM_BREACH_LOSS=28;

struct Vec { double p[6]{}; };
struct Draw { Counts hand{}, rest{}, discard{}; double prob{}; };
struct Key {
  int turn,hp,enemy,armor,vuln,weak,stance,potion_used,potion,relic,calm_exit;
  Counts hand,draw,discard;
  bool operator==(Key const& o) const {
    return turn==o.turn&&hp==o.hp&&enemy==o.enemy&&armor==o.armor&&vuln==o.vuln&&weak==o.weak&&stance==o.stance&&potion_used==o.potion_used&&potion==o.potion&&relic==o.relic&&calm_exit==o.calm_exit&&hand==o.hand&&draw==o.draw&&discard==o.discard;
  }
};
struct KeyHash {
  size_t operator()(Key const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.turn); mix(k.hp); mix(k.enemy); mix(k.armor); mix(k.vuln); mix(k.weak); mix(k.stance); mix(k.potion_used); mix(k.potion); mix(k.relic); mix(k.calm_exit);
    for(auto v:k.hand) mix(v); for(auto v:k.draw) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct Ctx {
  Counts hand{}, discard{};
  int enemy=0, armor=0, vuln=0, weak=0, stance=NORMAL, energy=3, block=0, potion_used=0, changed=0, mark_used=0, guard_used=0, played=0, calm_exit=0;
};
struct CtxKey {
  Counts hand{}, discard{};
  int enemy=0,armor=0,vuln=0,weak=0,stance=0,energy=0,block=0,potion_used=0,changed=0,mark_used=0,guard_used=0,played=0,calm_exit=0;
  bool operator==(CtxKey const& o) const {
    return enemy==o.enemy&&armor==o.armor&&vuln==o.vuln&&weak==o.weak&&stance==o.stance&&energy==o.energy&&block==o.block&&potion_used==o.potion_used&&changed==o.changed&&mark_used==o.mark_used&&guard_used==o.guard_used&&played==o.played&&calm_exit==o.calm_exit&&hand==o.hand&&discard==o.discard;
  }
};
struct CtxKeyHash {
  size_t operator()(CtxKey const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.enemy); mix(k.armor); mix(k.vuln); mix(k.weak); mix(k.stance); mix(k.energy); mix(k.block); mix(k.potion_used); mix(k.changed); mix(k.mark_used); mix(k.guard_used); mix(k.played); mix(k.calm_exit);
    for(auto v:k.hand) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct Row { Counts deck; int potion,relic,ft; Vec vec; double succ; std::string fam, display; };
static std::unordered_map<Key, Vec, KeyHash> memo;

int total(Counts const& c){ int s=0; for(auto v:c) s+=v; return s; }
Counts add(Counts a,Counts const& b){ for(int i=0;i<13;i++) a[i]+=b[i]; return a; }
Counts sub_one(Counts a,int c){ a[c]--; return a; }
Counts add_one(Counts a,int c){ a[c]++; return a; }
long long C(int n,int k){ if(k<0||k>n) return 0; if(k==0||k==n) return 1; long long r=1; for(int i=1;i<=k;i++) r=r*(n-k+i)/i; return r; }
double success(Vec const& v){ return v.p[0]+v.p[1]+v.p[2]+v.p[3]+v.p[4]; }
Vec fail(){ Vec v; v.p[5]=1; return v; }
Vec kill(int turn){ Vec v; v.p[turn-1]=1; return v; }
bool better(Vec const& a,Vec const& b,bool has){ if(!has) return true; if(std::abs(success(a)-success(b))>1e-12) return success(a)>success(b); for(int i=0;i<5;i++) if(std::abs(a.p[i]-b.p[i])>1e-12) return a.p[i]>b.p[i]; return false; }
Vec merge_vec(std::vector<std::pair<double,Vec>> const& items){ Vec out; for(auto const& it:items) for(int i=0;i<6;i++) out.p[i]+=it.first*it.second.p[i]; return out; }
void deal(int &enemy,int &armor,int amount){ int b=std::min(armor,amount); armor-=b; enemy-=amount-b; }
int weakd(int base,int weak){ return weak>0 ? (base*3)/4 : base; }

std::vector<Draw> draw_outcomes(Counts cards,int n,Counts fixed_discard={}) {
  std::vector<Draw> out; int m=total(cards);
  if(n>=m){ out.push_back({cards,{},fixed_discard,1.0}); return out; }
  long long denom=C(m,n); Counts pick{};
  std::function<void(int,int,long long)> rec=[&](int i,int left,long long ways){
    if(i==13){ if(left==0){ Counts rest{}; for(int j=0;j<13;j++) rest[j]=cards[j]-pick[j]; out.push_back({pick,rest,fixed_discard,(double)ways/(double)denom}); } return; }
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

void change_stance(Ctx &c,int ns,int relic) {
  if(c.stance==ns) return;
  if(c.stance==CALM) { c.energy += (relic==LOTUS ? 4 : 2); c.calm_exit++; }
  c.stance=ns; c.changed=1;
  if(ns==WRATH && relic==WRATH_GUARD && !c.guard_used){ c.block+=7; c.guard_used=1; }
}

void attack(Ctx &c,int base,int relic) {
  int dmg=base;
  if(relic==LOTUS && c.stance==CALM) dmg+=8;
  if(relic==STANCE_MARK && c.changed && !c.mark_used){ dmg+=5; c.mark_used=1; }
  if(c.vuln>0) dmg=(dmg*3)/2;
  if(c.stance==WRATH) dmg*=2;
  deal(c.enemy,c.armor,dmg);
}

bool play_card(Ctx const& in,int card,int relic,Ctx &out) {
  if(in.hand[card]==0 || in.energy<COST[card] || in.played>=2) return false;
  out=in; out.hand=sub_one(out.hand,card); out.energy-=COST[card]; out.played++; out.discard=add_one(out.discard,card);
  switch(card) {
    case ERUPTION: attack(out,9,relic); change_stance(out,WRATH,relic); break;
    case VIGILANCE: out.block+=8; change_stance(out,CALM,relic); break;
    case EMPTY_FIST: attack(out,9,relic); change_stance(out,NORMAL,relic); break;
    case EMPTY_BODY: out.block+=8; change_stance(out,NORMAL,relic); break;
    case FLURRY: attack(out,out.changed?8:4,relic); break;
    case WHEEL: attack(out,15,relic); break;
    case CONSECRATE: attack(out,5,relic); break;
    case CUT: attack(out,out.stance==CALM?11:6,relic); break;
    case BOWLING: attack(out,out.armor>0?12:8,relic); break;
    case PROTECT: out.block+=11; break;
    case STRIKE: attack(out,6,relic); break;
    case DEFEND: out.block+=5; break;
    case HALT: out.block += (out.stance==WRATH ? 9 : 4); break;
  }
  return true;
}

CtxKey make_ctx_key(Ctx const& c) {
  return {c.hand,c.discard,c.enemy,c.armor,c.vuln,c.weak,c.stance,c.energy,c.block,c.potion_used,c.changed,c.mark_used,c.guard_used,c.played,c.calm_exit};
}

bool use_potion(Ctx const& in,int potion,int relic,Ctx &out) {
  if(in.potion_used) return false;
  out=in; out.potion_used=1;
  if(potion==WRATH_POTION){ out.energy+=1; change_stance(out,WRATH,relic); }
  else if(potion==CALM_POTION){ out.block+=6; change_stance(out,CALM,relic); }
  else if(potion==FIRE_POTION){ deal(out.enemy,out.armor,18); }
  return true;
}

std::vector<Ctx> end_contexts(Key const& k) {
  Ctx start; start.hand=k.hand; start.discard=k.discard; start.enemy=k.enemy; start.armor=k.armor; start.vuln=k.vuln; start.weak=k.weak; start.stance=k.stance; start.potion_used=k.potion_used; start.calm_exit=k.calm_exit;
  std::vector<Ctx> finals;
  std::unordered_set<CtxKey,CtxKeyHash> action_seen;
  std::unordered_set<CtxKey,CtxKeyHash> final_seen;
  std::function<void(Ctx const&)> dfs = [&](Ctx const& c){
    CtxKey ak=make_ctx_key(c);
    if(!action_seen.insert(ak).second) return;

    Ctx stop=c;
    stop.discard=add(stop.discard,stop.hand);
    stop.hand={};
    stop.energy=0; stop.played=0; stop.changed=0; stop.mark_used=0; stop.guard_used=0;
    CtxKey fk=make_ctx_key(stop);
    if(final_seen.insert(fk).second) finals.push_back(stop);
    if(c.enemy<=0) return;

    if(!c.potion_used){
      Ctx p;
      if(use_potion(c,k.potion,k.relic,p)) dfs(p);
    }
    if(c.played>=2) return;
    for(int card=0;card<13;card++){
      if(c.hand[card]==0 || c.energy<COST[card]) continue;
      Ctx out;
      if(play_card(c,card,k.relic,out)) dfs(out);
    }
  };
  dfs(start);
  return finals;
}

Vec solve(Key k) {
  if(k.turn==4 && k.calm_exit>=2) k.enemy-=CALM_BREACH_LOSS;
  if(k.hp<=0) return fail();
  if(k.enemy<=0) return kill(k.turn);
  auto it=memo.find(k); if(it!=memo.end()) return it->second;
  Vec best; bool has=false;
  for(auto c:end_contexts(k)) {
    Vec cand;
    if(c.enemy<=0) cand=kill(k.turn);
    else {
      int incoming=weakd(DMG[k.turn-1],c.weak);
      if(c.stance==WRATH) incoming*=2;
      int hp2=k.hp-std::max(0,incoming-c.block);
      if(k.turn>=5 || hp2<=0) cand=fail();
      else {
        Counts discard2=add(c.discard,c.hand);
        std::vector<std::pair<double,Vec>> items;
        for(auto d:next_draws(k.draw,discard2,5)) {
          Key nk{k.turn+1,hp2,c.enemy,c.armor+ARMOR_GAIN[k.turn-1],std::max(0,c.vuln-1),std::max(0,c.weak-1),c.stance,c.potion_used,k.potion,k.relic,c.calm_exit,d.hand,d.rest,d.discard};
          items.push_back({d.prob,solve(nk)});
        }
        cand=merge_vec(items);
      }
    }
    if(better(cand,best,has)){ best=cand; has=true; }
  }
  if(!has) best=fail();
  memo.emplace(k,best); return best;
}

Vec result_for(Counts deck,int potion,int relic) {
  std::vector<std::pair<double,Vec>> items;
  for(auto d:draw_outcomes(deck,5)) {
    Key k{1,PLAYER_HP,ENEMY_HP,0,0,0,NORMAL,0,potion,relic,0,d.hand,d.rest,{}};
    items.push_back({d.prob,solve(k)});
  }
  return merge_vec(items);
}

void gen_decks_rec(int i,int left,Counts& cur,std::vector<Counts>& out) {
  if(i==13){
    if(left==10) out.push_back(cur);
    return;
  }
  for(int k=0;k<=POOL[i];k++){ cur[i]=k; gen_decks_rec(i+1,left+k,cur,out); }
  cur[i]=0;
}
std::vector<Counts> decks(){ std::vector<Counts> out; Counts c{}; gen_decks_rec(0,0,c,out); return out; }
int first_turn(Vec v){ for(int i=0;i<5;i++) if(v.p[i]>1e-9) return i+1; return 0; }
std::string deck_str(Counts d){ std::ostringstream os; bool first=true; for(int i=0;i<13;i++) if(d[i]){ if(!first) os<<"、"; first=false; os<<CN[i]; if(d[i]>1) os<<" x"<<(int)d[i]; } return os.str(); }
std::string family(Counts d,int p,int r){
  if(r==LOTUS && d[EMPTY_FIST]) return "紫莲花平静返能线";
  if(r==WRATH_GUARD && (p==WRATH_POTION || d[ERUPTION])) return "怒焰护符快线";
  if(r==STANCE_MARK && d[FLURRY]) return "姿态刻印追击线";
  if(p==CALM_POTION) return "平静药水稳线";
  if(p==FIRE_POTION) return "火焰补刀线";
  if(d[PROTECT]+d[DEFEND]+d[HALT]>=3) return "过量防守陷阱";
  return "混合线";
}

int env_int(const char* name,int fallback=0){
  const char* raw=std::getenv(name);
  return raw ? std::atoi(raw) : fallback;
}

int main(int argc,char** argv){
  if(argc>=18 && std::string(argv[1])=="build"){
    ENEMY_HP=std::atoi(argv[2]); PLAYER_HP=std::atoi(argv[3]);
    int potion=std::atoi(argv[4]), relic=std::atoi(argv[5]);
    Counts d{};
    for(int i=0;i<13;i++) d[i]=(unsigned char)std::atoi(argv[6+i]);
    Vec v=result_for(d,potion,relic);
    std::cout<<"enemy_hp "<<ENEMY_HP<<" player_hp "<<PLAYER_HP<<" success "<<success(v)*100<<" first "<<first_turn(v)<<" ["
      <<v.p[0]*100<<","<<v.p[1]*100<<","<<v.p[2]*100<<","<<v.p[3]*100<<","<<v.p[4]*100<<","<<v.p[5]*100<<"] "
      <<family(d,potion,relic)<<" "<<deck_str(d)<<"；"<<POTION_CN[potion]<<"；"<<RELIC_CN[relic]<<"\n";
    return 0;
  }
  if(argc>=3){ ENEMY_HP=std::atoi(argv[1]); PLAYER_HP=std::atoi(argv[2]); }
  int max_builds=env_int("GONGDOU_AUDIT_MAX_BUILDS",0);
  int skip_builds=env_int("GONGDOU_AUDIT_SKIP_BUILDS",0);
  int progress_every=env_int("GONGDOU_AUDIT_PROGRESS_EVERY",200);
  bool no_write=env_int("GONGDOU_AUDIT_NO_WRITE",0)==1;
  auto ds=decks(); std::vector<Row> rows; int done=0,seen=0,total_builds=(int)ds.size()*9; bool stop=false;
  for(auto d:ds){ for(int p=0;p<3;p++){ for(int r=0;r<3;r++){
    if(seen++<skip_builds) continue;
    Vec v=result_for(d,p,r);
    rows.push_back({d,p,r,first_turn(v),v,success(v),family(d,p,r),deck_str(d)+"；"+POTION_CN[p]+"；"+RELIC_CN[r]});
    done++;
    if(progress_every>0 && (done%progress_every==0 || done==total_builds)){ std::cout<<"audited "<<done<<"/"<<total_builds<<"\n"; std::cout.flush(); }
    if(max_builds>0 && done>=max_builds){ stop=true; break; }
  } if(stop) break; } if(stop) break; }
  std::sort(rows.begin(),rows.end(),[](auto&a,auto&b){ if(std::abs(a.succ-b.succ)>1e-12) return a.succ>b.succ; int af=a.ft?a.ft:99,bf=b.ft?b.ft:99; if(af!=bf) return af<bf; return a.vec.p[2]>b.vec.p[2]; });
  int perfect=0; for(auto&r:rows) if(r.succ>0.999999) perfect++;
  Row best_turn[6]; bool has_turn[6]{};
  std::vector<std::string> fam_names={"紫莲花平静返能线","怒焰护符快线","姿态刻印追击线","平静药水稳线","火焰补刀线","过量防守陷阱","混合线"};
  std::vector<Row> best_fam(fam_names.size()); std::vector<bool> has_fam(fam_names.size(),false);
  for(auto const& r:rows){
    int t=r.ft; if(t>=0&&t<=5&&(!has_turn[t]||r.succ>best_turn[t].succ)){ best_turn[t]=r; has_turn[t]=true; }
    for(int i=0;i<(int)fam_names.size();i++) if(r.fam==fam_names[i]&&(!has_fam[i]||r.succ>best_fam[i].succ)){ best_fam[i]=r; has_fam[i]=true; }
  }
  std::string suffix=max_builds>0 ? ("_part_"+std::to_string(skip_builds)+"_"+std::to_string(rows.size())) : "";
  if(!no_write){
    std::ofstream jf("difficulty6_stance_deadline_audit"+suffix+".json");
    jf<<"{\"audit_model_version\":\"full_action_search_v1\",\"decision_model\":\"full_action_search\",\"draw_model\":\"exact_without_fixed_priority\",\"enemy_hp\":"<<ENEMY_HP<<",\"player_hp\":"<<PLAYER_HP<<",\"legal_deck_count\":"<<ds.size()<<",\"total_build_count\":"<<total_builds<<",\"skip_builds\":"<<skip_builds<<",\"legal_build_count\":"<<rows.size()<<",\"perfect_success_count\":"<<perfect<<",\"top30\":[";
    for(int i=0;i<30&&i<(int)rows.size();i++){ if(i) jf<<","; auto&r=rows[i]; jf<<"{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"]}"; }
    jf<<"],\"best_by_first_turn\":{"; bool first=true; for(int t=0;t<=5;t++) if(has_turn[t]){ if(!first) jf<<","; first=false; auto&r=best_turn[t]; jf<<"\""<<t<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"]}"; }
    jf<<"},\"best_by_family\":{"; first=true; for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ if(!first) jf<<","; first=false; auto&r=best_fam[i]; jf<<"\""<<fam_names[i]<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"]}"; }
    jf<<"}}"; jf.close();
  }
  if(rows.empty()){
    std::cout<<"enemy_hp "<<ENEMY_HP<<" player_hp "<<PLAYER_HP<<"\nlegal_deck_count "<<ds.size()<<"\nlegal_build_count 0\nperfect_success_count 0\nno rows audited\n";
    return 0;
  }
  auto&r=rows.front();
  std::cout<<"enemy_hp "<<ENEMY_HP<<" player_hp "<<PLAYER_HP<<"\nlegal_deck_count "<<ds.size()<<"\nlegal_build_count "<<rows.size()<<"\nperfect_success_count "<<perfect<<"\n";
  std::cout<<"best "<<r.succ*100<<" first "<<r.ft<<" "<<r.fam<<" ["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"] "<<r.display<<"\n";
  std::cout<<"best_by_first_turn\n";
  for(int t=0;t<=5;t++) if(has_turn[t]){ auto&b=best_turn[t]; std::cout<<t<<" "<<b.succ*100<<" "<<b.fam<<" ["<<b.vec.p[0]*100<<","<<b.vec.p[1]*100<<","<<b.vec.p[2]*100<<","<<b.vec.p[3]*100<<","<<b.vec.p[4]*100<<","<<b.vec.p[5]*100<<"] "<<b.display<<"\n"; }
  std::cout<<"best_by_family\n";
  for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ auto&b=best_fam[i]; std::cout<<fam_names[i]<<" "<<b.succ*100<<" first "<<b.ft<<" ["<<b.vec.p[0]*100<<","<<b.vec.p[1]*100<<","<<b.vec.p[2]*100<<","<<b.vec.p[3]*100<<","<<b.vec.p[4]*100<<","<<b.vec.p[5]*100<<"] "<<b.display<<"\n"; }
}
