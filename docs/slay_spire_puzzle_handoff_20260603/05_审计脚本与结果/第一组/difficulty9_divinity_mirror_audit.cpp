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

using Counts = std::array<unsigned char, 15>;
enum Card { DEVOTION, PROSTRATE, PRAY, WORSHIP, BRILLIANCE, RAGNAROK, JUDGMENT, CARVE, SMITE, CONSECRATE, SANCTITY, WALL, STRIKE, DEFEND, EMPTY_BODY };
enum Potion { AMBROSIA, MANTRA_POTION, MIRROR_POTION };
enum Relic { DAMARU, SCRIPTURE, SUN_DIAL };

const int POOL[15] = {1,2,2,2,2,1,1,2,2,2,2,2,3,2,2};
const int COST[15] = {1,0,1,2,1,2,1,1,1,0,1,2,1,1,1};
const char* CN[15] = {"虔信（改）","五体投地（改）","祈祷（改）","敬拜（改）","光辉（改）","诸神黄昏（改）","审判（改）","刻现实（改）","惩击","供奉（改）","圣洁（改）","护墙","打击","防御","化体（改）"};
const char* POTION_CN[3] = {"神格药水","真言药水","破镜药水"};
const char* RELIC_CN[3] = {"达玛鲁","经文残页","日晷碎片"};

int ENEMY_HP=112, PLAYER_HP=38;
int DMG[5]={15,24,34,34,34};
int ARMOR_GAIN[5]={0,24,0,0,0};

struct Vec { double p[6]{}; };
struct Draw { Counts hand{}, rest{}, discard{}; double prob{}; };
struct DrawKey {
  Counts cards{}, fixed_discard{};
  int n=0;
  bool operator==(DrawKey const& o) const { return n==o.n && cards==o.cards && fixed_discard==o.fixed_discard; }
};
struct DrawKeyHash {
  size_t operator()(DrawKey const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.n);
    for(auto v:k.cards) mix(v);
    for(auto v:k.fixed_discard) mix(v);
    return h;
  }
};
static std::unordered_map<DrawKey, std::vector<Draw>, DrawKeyHash> draw_memo;
struct Key {
  int turn,hp,enemy,armor,mantra,devotion,potion_used,potion,relic,sun_used;
  Counts hand,draw,discard;
  bool operator==(Key const& o) const {
    return turn==o.turn&&hp==o.hp&&enemy==o.enemy&&armor==o.armor&&mantra==o.mantra&&devotion==o.devotion&&potion_used==o.potion_used&&potion==o.potion&&relic==o.relic&&sun_used==o.sun_used&&hand==o.hand&&draw==o.draw&&discard==o.discard;
  }
};
struct KeyHash {
  size_t operator()(Key const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.turn); mix(k.hp); mix(k.enemy); mix(k.armor); mix(k.mantra); mix(k.devotion); mix(k.potion_used); mix(k.potion); mix(k.relic); mix(k.sun_used);
    for(auto v:k.hand) mix(v); for(auto v:k.draw) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct Ctx {
  Counts hand{}, discard{};
  int enemy=0, armor=0, mantra=0, devotion=0, potion_used=0, relic=0, sun_used=0, energy=3, block=0, plays=0, divinity=0, entered_divinity=0;
};
struct CtxKey {
  Counts hand{}, discard{};
  int enemy=0,armor=0,mantra=0,devotion=0,potion_used=0,relic=0,sun_used=0,energy=0,block=0,plays=0,divinity=0,entered_divinity=0;
  bool operator==(CtxKey const& o) const {
    return enemy==o.enemy&&armor==o.armor&&mantra==o.mantra&&devotion==o.devotion&&potion_used==o.potion_used&&relic==o.relic&&sun_used==o.sun_used&&energy==o.energy&&block==o.block&&plays==o.plays&&divinity==o.divinity&&entered_divinity==o.entered_divinity&&hand==o.hand&&discard==o.discard;
  }
};
struct CtxKeyHash {
  size_t operator()(CtxKey const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.enemy); mix(k.armor); mix(k.mantra); mix(k.devotion); mix(k.potion_used); mix(k.relic); mix(k.sun_used); mix(k.energy); mix(k.block); mix(k.plays); mix(k.divinity); mix(k.entered_divinity);
    for(auto v:k.hand) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct Row { Counts deck; int potion,relic,ft; Vec vec; double succ; std::string fam,display; };
static std::unordered_map<Key, Vec, KeyHash> memo;
static Counts CURRENT_DECK{};
static std::vector<Draw> CURRENT_DRAWS;

int total(Counts const& c){ int s=0; for(auto v:c) s+=v; return s; }
Counts add(Counts a, Counts const& b){ for(int i=0;i<15;i++) a[i]+=b[i]; return a; }
Counts sub_one(Counts a,int c){ a[c]--; return a; }
Counts add_one(Counts a,int c){ a[c]++; return a; }
long long C(int n,int k){ if(k<0||k>n) return 0; if(k==0||k==n) return 1; long long r=1; for(int i=1;i<=k;i++) r=r*(n-k+i)/i; return r; }
double success(Vec const& v){ return v.p[0]+v.p[1]+v.p[2]+v.p[3]+v.p[4]; }
Vec fail(){ Vec v; v.p[5]=1; return v; }
Vec kill(int turn){ Vec v; v.p[turn-1]=1; return v; }
bool better(Vec const& a,Vec const& b,bool has){ if(!has) return true; if(std::abs(success(a)-success(b))>1e-12) return success(a)>success(b); for(int i=0;i<5;i++) if(std::abs(a.p[i]-b.p[i])>1e-12) return a.p[i]>b.p[i]; return false; }
Vec merge_vec(std::vector<std::pair<double,Vec>> const& items){ Vec out; for(auto const& it:items) for(int i=0;i<6;i++) out.p[i]+=it.first*it.second.p[i]; return out; }

void gain_mantra(Ctx &c,int amount){
  if(c.relic==SCRIPTURE && amount>0) amount+=1;
  c.mantra+=amount;
  if(c.mantra>=10 && !c.divinity){
    c.mantra-=10;
    c.divinity=1;
    c.entered_divinity=1;
  }
  if(c.mantra>18) c.mantra=18;
}

void deal(Ctx &c,int amount,bool attack=true){
  int raw=amount;
  if(c.divinity && attack) raw*=3;
  if(c.relic==SUN_DIAL && c.entered_divinity && attack && !c.sun_used){ raw+=15; c.sun_used=1; }
  if(attack && !c.divinity && raw>=12) c.armor+=10;
  int b=std::min(c.armor,raw);
  c.armor-=b; c.enemy-=raw-b;
}

std::vector<Draw> draw_outcomes(Counts cards,int n,Counts fixed_discard={}){
  DrawKey key{cards,fixed_discard,n};
  auto memo_it=draw_memo.find(key);
  if(memo_it!=draw_memo.end()) return memo_it->second;
  std::vector<Draw> out; int m=total(cards);
  if(n>=m){ out.push_back({cards,{},fixed_discard,1.0}); draw_memo.emplace(key,out); return out; }
  long long denom=C(m,n); Counts pick{};
  std::function<void(int,int,long long)> rec=[&](int i,int left,long long ways){
    if(i==15){ if(left==0){ Counts rest{}; for(int j=0;j<15;j++) rest[j]=cards[j]-pick[j]; out.push_back({pick,rest,fixed_discard,(double)ways/(double)denom}); } return; }
    int mx=std::min<int>(cards[i],left);
    for(int k=0;k<=mx;k++){ pick[i]=k; rec(i+1,left-k,ways*C(cards[i],k)); }
    pick[i]=0;
  };
  rec(0,n,1); draw_memo.emplace(key,out); return out;
}
std::vector<Draw> next_draws(Counts draw,Counts discard,int n){
  if(total(draw)>=n) return draw_outcomes(draw,n,discard);
  Counts fixed=draw; int need=n-total(draw);
  if(total(discard)==0) return {{fixed,{}, {},1.0}};
  auto tmp=draw_outcomes(discard,need,{});
  for(auto &d:tmp) d.hand=add(fixed,d.hand);
  return tmp;
}

bool play_card(Ctx const& in,int card,Ctx &out){
  if(in.hand[card]==0 || in.energy<COST[card] || in.plays>=2) return false;
  out=in; out.hand=sub_one(out.hand,card); out.energy-=COST[card]; out.plays++;
  bool exhaust=false;
  switch(card){
    case DEVOTION: gain_mantra(out,4); exhaust=true; break;
    case PROSTRATE: out.block+=4; gain_mantra(out,2); break;
    case PRAY: out.block+=5; gain_mantra(out,3); break;
    case WORSHIP: gain_mantra(out,5); break;
    case BRILLIANCE: deal(out,8+2*out.mantra,true); break;
    case RAGNAROK: for(int i=0;i<4;i++) deal(out,5,true); break;
    case JUDGMENT: if(out.enemy<=30) out.enemy=0; else { deal(out,5,true); out.armor+=16; } break;
    case CARVE: deal(out,6,true); break;
    case SMITE: deal(out,12,true); break;
    case CONSECRATE: deal(out,5,true); break;
    case SANCTITY: out.block+=8; if(out.mantra>=5) out.block+=4; break;
    case WALL: out.block+=13; break;
    case STRIKE: deal(out,6,true); break;
    case DEFEND: out.block+=5; break;
    case EMPTY_BODY: out.block+=7; if(out.divinity){ out.energy+=1; out.divinity=0; } break;
  }
  if(!exhaust) out.discard=add_one(out.discard,card);
  return true;
}

bool use_potion(Ctx const& in,int potion,Ctx &out){
  if(in.potion_used) return false;
  out=in; out.potion_used=1;
  if(potion==AMBROSIA){ out.divinity=1; out.entered_divinity=1; }
  else if(potion==MANTRA_POTION) gain_mantra(out,6);
  else if(potion==MIRROR_POTION) out.armor=std::max(0,out.armor-20);
  return true;
}

std::vector<Ctx> end_contexts(Key const& k){
  Ctx start; start.hand=k.hand; start.discard=k.discard; start.enemy=k.enemy; start.armor=k.armor; start.mantra=k.mantra; start.devotion=k.devotion; start.potion_used=k.potion_used; start.relic=k.relic; start.sun_used=k.sun_used;
  if(start.relic==DAMARU) gain_mantra(start,2);
  std::vector<Ctx> uniq; std::unordered_set<CtxKey, CtxKeyHash> final_seen, action_seen;
  auto push_final=[&](Ctx c){
    c.discard=add(c.discard,c.hand);
    c.hand={};
    c.energy=0;
    c.plays=0;
    c.divinity=0;
    CtxKey ck{c.hand,c.discard,c.enemy,c.armor,c.mantra,c.devotion,c.potion_used,c.relic,c.sun_used,c.energy,c.block,c.plays,c.divinity,c.entered_divinity};
    if(final_seen.insert(ck).second) uniq.push_back(c);
  };
  std::function<void(Ctx const&)> bestFromCtx=[&](Ctx const& c){
    CtxKey ak{c.hand,c.discard,c.enemy,c.armor,c.mantra,c.devotion,c.potion_used,c.relic,c.sun_used,c.energy,c.block,c.plays,c.divinity,c.entered_divinity};
    if(!action_seen.insert(ak).second) return;
    push_final(c);
    if(c.enemy<=0) return;
    if(!c.potion_used){
      Ctx out;
      if(use_potion(c,k.potion,out)) bestFromCtx(out);
    }
    if(c.plays>=2) return;
    for(int card=0;card<15;card++){
      Ctx out;
      if(play_card(c,card,out)) bestFromCtx(out);
    }
  };
  bestFromCtx(start);
  return uniq;
}

Vec solve(Key k){
  if(k.hp<=0) return fail();
  if(k.enemy<=0) return kill(k.turn);
  auto it=memo.find(k); if(it!=memo.end()) return it->second;
  Vec best; bool has=false;
  for(auto c:end_contexts(k)){
    Vec cand;
    if(c.enemy<=0) cand=kill(k.turn);
    else{
      int hp2=k.hp-std::max(0,DMG[k.turn-1]-c.block);
      int armor2=c.armor+ARMOR_GAIN[k.turn-1];
      if(k.turn>=5 || hp2<=0) cand=fail();
      else{
        std::vector<std::pair<double,Vec>> items;
        for(auto const& d:CURRENT_DRAWS){
          Key nk{k.turn+1,hp2,c.enemy,armor2,c.mantra,c.devotion,c.potion_used,k.potion,k.relic,c.sun_used,d.hand,{},{}};
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

Vec result_for(Counts deck,int potion,int relic){
  CURRENT_DECK=deck;
  CURRENT_DRAWS=draw_outcomes(deck,3);
  std::vector<std::pair<double,Vec>> items;
  for(auto const& d:CURRENT_DRAWS){
    Key k{1,PLAYER_HP,ENEMY_HP,0,0,0,0,potion,relic,0,d.hand,{},{}};
    items.push_back({d.prob,solve(k)});
  }
  return merge_vec(items);
}

void gen_decks_rec(int i,int left,Counts& cur,std::vector<Counts>& out){
  if(i==15){
    int mantra=cur[PROSTRATE]+cur[PRAY]+cur[WORSHIP]+cur[DEVOTION];
    int attack=cur[BRILLIANCE]+cur[RAGNAROK]+cur[JUDGMENT]+cur[CARVE]+cur[SMITE]+cur[CONSECRATE]+cur[STRIKE];
    int defense=cur[PROSTRATE]+cur[PRAY]+cur[SANCTITY]+cur[WALL]+cur[DEFEND]+cur[EMPTY_BODY];
    if(left==13 && cur[DEVOTION]==1 && cur[PROSTRATE]==2 && cur[WORSHIP]==2 && cur[PRAY]>=1 && cur[BRILLIANCE]>=1 && cur[RAGNAROK]==1 && cur[JUDGMENT]==1 && cur[SANCTITY]>=1 && mantra>=5 && attack>=5 && defense>=4) out.push_back(cur);
    return;
  }
  for(int k=0;k<=POOL[i] && left+k<=13;k++){ cur[i]=k; gen_decks_rec(i+1,left+k,cur,out); }
  cur[i]=0;
}
std::vector<Counts> decks(){ std::vector<Counts> out; Counts c{}; gen_decks_rec(0,0,c,out); return out; }
int first_turn(Vec v){ for(int i=0;i<5;i++) if(v.p[i]>1e-9) return i+1; return 0; }
std::string deck_str(Counts d){ std::ostringstream os; bool first=true; for(int i=0;i<15;i++) if(d[i]){ if(!first) os<<"、"; first=false; os<<CN[i]; if(d[i]>1) os<<" x"<<(int)d[i]; } return os.str(); }
std::string family(Counts d,int p,int r){
  if(p==AMBROSIA && d[RAGNAROK]) return "神格药水爆发线";
  if(r==DAMARU || p==MANTRA_POTION) return "真言蓄力线";
  if(r==SUN_DIAL && d[BRILLIANCE]) return "日晷光辉线";
  if(p==MIRROR_POTION) return "破镜护盾线";
  return "混合线";
}

int main(int argc,char** argv){
  if(argc>=20 && std::string(argv[1])=="build"){
    ENEMY_HP=std::atoi(argv[2]); PLAYER_HP=std::atoi(argv[3]);
    int potion=std::atoi(argv[4]), relic=std::atoi(argv[5]);
    Counts d{}; for(int i=0;i<15;i++) d[i]=(unsigned char)std::atoi(argv[6+i]);
    Vec v=result_for(d,potion,relic);
    std::cout<<"enemy_hp "<<ENEMY_HP<<" player_hp "<<PLAYER_HP<<" success "<<success(v)*100<<" first "<<first_turn(v)<<" ["
      <<v.p[0]*100<<","<<v.p[1]*100<<","<<v.p[2]*100<<","<<v.p[3]*100<<","<<v.p[4]*100<<","<<v.p[5]*100<<"] "
      <<family(d,potion,relic)<<" "<<deck_str(d)<<"；"<<POTION_CN[potion]<<"；"<<RELIC_CN[relic]<<"\n";
    return 0;
  }
  if(argc>=3){ ENEMY_HP=std::atoi(argv[1]); PLAYER_HP=std::atoi(argv[2]); }
  auto env_int=[](const char* name,int fallback){ const char* v=std::getenv(name); return v ? std::atoi(v) : fallback; };
  int max_builds=argc>=4?std::atoi(argv[3]):env_int("GONGDOU_AUDIT_MAX_BUILDS",0);
  int skip_builds=argc>=5?std::atoi(argv[4]):env_int("GONGDOU_AUDIT_SKIP_BUILDS",0);
  int progress_every=env_int("GONGDOU_AUDIT_PROGRESS_EVERY",50);
  bool no_write=std::getenv("GONGDOU_AUDIT_NO_WRITE") && std::string(std::getenv("GONGDOU_AUDIT_NO_WRITE"))=="1";
  auto ds=decks(); std::vector<Row> rows; int done=0,seen=0,total_builds=(int)ds.size()*9;
  for(auto d:ds){
    memo.clear();
    for(int p=0;p<3;p++) for(int r=0;r<3;r++){
    if(seen++<skip_builds) continue;
    Vec v=result_for(d,p,r);
    rows.push_back({d,p,r,first_turn(v),v,success(v),family(d,p,r),deck_str(d)+"；"+POTION_CN[p]+"；"+RELIC_CN[r]});
    done++;
    if(progress_every>0 && (done%progress_every==0||seen==total_builds)){ auto best_it=std::max_element(rows.begin(),rows.end(),[](auto const&a,auto const&b){return a.succ<b.succ;}); std::cout<<"audited "<<done<<"/"<<total_builds; if(best_it!=rows.end()) std::cout<<" best_so_far "<<best_it->succ*100<<" first "<<best_it->ft<<" "<<best_it->fam; std::cout<<"\n"; std::cout.flush(); }
    if(max_builds && done>=max_builds) goto done_loop;
  }
  }
done_loop:
  std::sort(rows.begin(),rows.end(),[](auto&a,auto&b){ if(std::abs(a.succ-b.succ)>1e-12) return a.succ>b.succ; int af=a.ft?a.ft:99,bf=b.ft?b.ft:99; if(af!=bf) return af<bf; return a.vec.p[2]>b.vec.p[2]; });
  int perfect=0; for(auto&r:rows) if(r.succ>0.999999) perfect++;
  Row best_turn[6]; bool has_turn[6]{};
  std::vector<std::string> fam_names={"神格药水爆发线","真言蓄力线","日晷光辉线","破镜护盾线","混合线"};
  std::vector<Row> best_fam(fam_names.size()); std::vector<bool> has_fam(fam_names.size(),false);
  for(auto const& r:rows){
    int t=r.ft; if(t>=0&&t<=5&&(!has_turn[t]||r.succ>best_turn[t].succ)){ best_turn[t]=r; has_turn[t]=true; }
    for(int i=0;i<(int)fam_names.size();i++) if(r.fam==fam_names[i]&&(!has_fam[i]||r.succ>best_fam[i].succ)){ best_fam[i]=r; has_fam[i]=true; }
  }
  std::ostringstream fn; fn<<"difficulty9_divinity_mirror_audit"; if(skip_builds||max_builds) fn<<"_part_"<<skip_builds<<"_"<<rows.size(); fn<<".json";
  if(!no_write){
  std::ofstream jf(fn.str());
  jf<<"{\"enemy_hp\":"<<ENEMY_HP<<",\"player_hp\":"<<PLAYER_HP<<",\"decision_model\":\"full_action_search\",\"legal_deck_count\":"<<ds.size()<<",\"total_build_count\":"<<total_builds<<",\"skip_builds\":"<<skip_builds<<",\"legal_build_count\":"<<rows.size()<<",\"perfect_success_count\":"<<perfect<<",\"top30\":[";
  for(int i=0;i<30&&i<(int)rows.size();i++){ if(i) jf<<","; auto&r=rows[i]; jf<<"{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"]}"; }
  jf<<"],\"best_by_first_turn\":{"; bool first=true; for(int t=0;t<=5;t++) if(has_turn[t]){ if(!first) jf<<","; first=false; auto&r=best_turn[t]; jf<<"\""<<t<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"]}"; }
  jf<<"},\"best_by_family\":{"; first=true; for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ if(!first) jf<<","; first=false; auto&r=best_fam[i]; jf<<"\""<<fam_names[i]<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"]}"; }
  jf<<"}}"; jf.close();
  }
  auto&r=rows.front();
  std::cout<<"enemy_hp "<<ENEMY_HP<<" player_hp "<<PLAYER_HP<<"\nlegal_deck_count "<<ds.size()<<"\nlegal_build_count "<<rows.size()<<"\nperfect_success_count "<<perfect<<"\n";
  std::cout<<"best "<<r.succ*100<<" first "<<r.ft<<" "<<r.fam<<" ["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"] "<<r.display<<"\n";
  std::cout<<"best_by_first_turn\n";
  for(int t=0;t<=5;t++) if(has_turn[t]){ auto&b=best_turn[t]; std::cout<<t<<" "<<b.succ*100<<" "<<b.fam<<" ["<<b.vec.p[0]*100<<","<<b.vec.p[1]*100<<","<<b.vec.p[2]*100<<","<<b.vec.p[3]*100<<","<<b.vec.p[4]*100<<","<<b.vec.p[5]*100<<"] "<<b.display<<"\n"; }
  std::cout<<"best_by_family\n";
  for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ auto&b=best_fam[i]; std::cout<<fam_names[i]<<" "<<b.succ*100<<" first "<<b.ft<<" ["<<b.vec.p[0]*100<<","<<b.vec.p[1]*100<<","<<b.vec.p[2]*100<<","<<b.vec.p[3]*100<<","<<b.vec.p[4]*100<<","<<b.vec.p[5]*100<<"] "<<b.display<<"\n"; }
}
