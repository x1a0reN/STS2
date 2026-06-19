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
#include <vector>

using Counts = std::array<unsigned char, 13>;
enum Card { BASH, UPPERCUT, NEUTRALIZE, CLOTHESLINE, VOID_REND, DAGGER, QUICK, BALL, COLD, SWORD, STRIKE, DEFEND, VOID };
enum Potion { FIRE, BREAKER, ENERGY_POTION };
enum Relic { SHURIKEN, ANCHOR, VOID_LENS };

const int POOL[13] = {1,1,1,1,1,1,2,2,1,1,3,3,0};
const int COST[13] = {2,2,0,2,1,1,1,1,1,1,1,1,99};
const char* CN[13] = {"重击","上勾拳","中和","金刚臂","虚空裂解（改）","投掷匕首（改）","快斩（改）","球状闪电（改）","寒流（改）","飞剑回旋","打击","防御","虚空"};
const char* POTION_CN[3] = {"火焰药水","破障药水","能量药水"};
const char* RELIC_CN[3] = {"手里剑","锚","虚空透镜"};

int ENEMY_HP=92, PLAYER_HP=24;
int DMG[4]={8,15,23,34};
int ARMOR_GAIN[4]={0,10,0,16};
int VOID_GAIN[4]={0,1,1,0};

struct Vec { double p[5]{}; };
struct Draw { Counts hand{}, rest{}, discard{}; double prob{}; };
struct Key {
  int turn,hp,enemy,armor,artifact,vuln,weak,strength,potion_used,potion,relic;
  Counts hand,draw,discard;
  bool operator==(Key const& o) const {
    return turn==o.turn&&hp==o.hp&&enemy==o.enemy&&armor==o.armor&&artifact==o.artifact&&vuln==o.vuln&&weak==o.weak&&strength==o.strength&&potion_used==o.potion_used&&potion==o.potion&&relic==o.relic&&hand==o.hand&&draw==o.draw&&discard==o.discard;
  }
};
struct KeyHash {
  size_t operator()(Key const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.turn); mix(k.hp); mix(k.enemy); mix(k.armor); mix(k.artifact); mix(k.vuln); mix(k.weak); mix(k.strength); mix(k.potion_used); mix(k.potion); mix(k.relic);
    for(auto v:k.hand) mix(v); for(auto v:k.draw) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct Row { Counts deck; int potion,relic,ft; Vec vec; double succ; std::string fam, display; };
static std::unordered_map<Key, Vec, KeyHash> memo;

int total(Counts const& c){ int s=0; for(auto v:c) s+=v; return s; }
Counts add(Counts a, Counts const& b){ for(int i=0;i<13;i++) a[i]+=b[i]; return a; }
Counts add_one(Counts a,int c){ a[c]++; return a; }
long long C(int n,int k){ if(k<0||k>n) return 0; if(k==0||k==n) return 1; long long r=1; for(int i=1;i<=k;i++) r=r*(n-k+i)/i; return r; }
double success(Vec const& v){ return v.p[0]+v.p[1]+v.p[2]+v.p[3]; }
Vec fail(){ Vec v; v.p[4]=1; return v; }
Vec kill(int turn){ Vec v; v.p[turn-1]=1; return v; }
bool better(Vec const& a, Vec const& b, bool has){ if(!has) return true; if(std::abs(success(a)-success(b))>1e-12) return success(a)>success(b); for(int i=0;i<4;i++) if(std::abs(a.p[i]-b.p[i])>1e-12) return a.p[i]>b.p[i]; return false; }
Vec merge_vec(std::vector<std::pair<double,Vec>> const& items){ Vec out; for(auto const& it:items) for(int i=0;i<5;i++) out.p[i]+=it.first*it.second.p[i]; return out; }
int weakd(int base,int weak){ return weak>0 ? (base*3)/4 : base; }
int atk(int base,int str,int vuln){ int v=base+str; return vuln>0 ? (v*3)/2 : v; }
void deal(int &enemy,int &armor,int amount){ int b=std::min(armor,amount); armor-=b; enemy-=amount-b; }
void apply_status(int &artifact,int &status,int n){ if(artifact>0) artifact--; else status+=n; }

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

std::vector<Counts> maximal_subsets(Counts hand,int energy) {
  std::vector<Counts> out; Counts cur{};
  std::function<void(int,int,int)> rec=[&](int i,int spent,int used){
    if(i==12){
      bool can_add=false;
      for(int c=0;c<12;c++) if(cur[c]<hand[c] && spent+COST[c]<=energy && used<2) can_add=true;
      if(!can_add) out.push_back(cur);
      return;
    }
    int mx=hand[i];
    for(int k=0;k<=mx && used+k<=2;k++){ int ns=spent+k*COST[i]; if(ns<=energy){ cur[i]=k; rec(i+1,ns,used+k); } }
    cur[i]=0;
  };
  rec(0,0,0); return out;
}

Vec solve(Key k) {
  if(k.hp<=0) return fail();
  if(k.enemy<=0) return kill(k.turn);
  auto it=memo.find(k); if(it!=memo.end()) return it->second;
  Vec best; bool has=false;
  int base_energy=std::max(0,3-(int)k.hand[VOID]);
  struct Start{int enemy,armor,artifact,vuln,weak,energy,potion_used;};
  std::vector<Start> starts{{k.enemy,k.armor,k.artifact,k.vuln,k.weak,base_energy,k.potion_used}};
  if(!k.potion_used){
    Start p=starts[0]; p.potion_used=1;
    if(k.potion==FIRE) deal(p.enemy,p.armor,16);
    else if(k.potion==BREAKER){ p.artifact=std::max(0,p.artifact-2); p.vuln+=2; }
    else if(k.potion==ENERGY_POTION) p.energy+=3;
    starts.push_back(p);
  }
  for(auto s:starts){
    int base_block=(k.relic==ANCHOR && k.turn==1) ? 10 : 0;
    for(auto sub:maximal_subsets(k.hand,s.energy)){
      Counts hand_left=k.hand, discard_after=k.discard;
      for(int c=0;c<12;c++){ hand_left[c]-=sub[c]; discard_after[c]+=sub[c]; }
      int enemy=s.enemy, armor=s.armor, artifact=s.artifact, vuln=s.vuln, weak=s.weak, str=k.strength, block=base_block;
      int attacks=0; bool lens=false;
      auto attack=[&](int base){ deal(enemy,armor,atk(base,str,vuln)); attacks++; if(k.relic==SHURIKEN && attacks==3) str+=1; };
      for(int i=0;i<sub[BASH];i++){ attack(8); apply_status(artifact,vuln,2); }
      for(int i=0;i<sub[UPPERCUT];i++){ attack(13); apply_status(artifact,vuln,1); apply_status(artifact,weak,1); }
      for(int i=0;i<sub[NEUTRALIZE];i++){ attack(3); apply_status(artifact,weak,1); }
      for(int i=0;i<sub[CLOTHESLINE];i++){ attack(12); apply_status(artifact,weak,2); }
      for(int i=0;i<sub[VOID_REND];i++){ int d=8; if(hand_left[VOID]>0){ hand_left[VOID]--; d=22; if(k.relic==VOID_LENS && !lens){ d+=24; lens=true; } } attack(d); }
      for(int i=0;i<sub[DAGGER];i++) attack(9);
      for(int i=0;i<sub[QUICK];i++) attack(8);
      for(int i=0;i<sub[BALL];i++) attack(7);
      for(int i=0;i<sub[SWORD];i++){ attack(3); attack(3); attack(3); }
      for(int i=0;i<sub[COLD];i++){ attack(6); block+=4; }
      for(int i=0;i<sub[STRIKE];i++) attack(6);
      for(int i=0;i<sub[DEFEND];i++) block+=5;
      Vec cand;
      if(enemy<=0) cand=kill(k.turn);
      else {
        int incoming=weakd(DMG[k.turn-1],weak);
        int hp2=k.hp-std::max(0,incoming-block);
        if(k.turn>=4 || hp2<=0) cand=fail();
        else{
          Counts discard2=add(discard_after,hand_left);
          for(int i=0;i<VOID_GAIN[k.turn-1];i++) discard2=add_one(discard2,VOID);
          std::vector<std::pair<double,Vec>> items;
          for(auto d:next_draws(k.draw,discard2,5)){
            Key nk{k.turn+1,hp2,enemy,armor+ARMOR_GAIN[k.turn-1],artifact,std::max(0,vuln-1),std::max(0,weak-1),str,s.potion_used,k.potion,k.relic,d.hand,d.rest,d.discard};
            items.push_back({d.prob,solve(nk)});
          }
          cand=merge_vec(items);
        }
      }
      if(better(cand,best,has)){ best=cand; has=true; }
    }
  }
  if(!has) best=fail();
  memo.emplace(k,best); return best;
}

Vec result_for(Counts deck,int potion,int relic){
  std::vector<std::pair<double,Vec>> items;
  for(auto d:draw_outcomes(deck,5)){
    Key k{1,PLAYER_HP,ENEMY_HP,0,2,0,0,0,0,potion,relic,d.hand,d.rest,{}};
    items.push_back({d.prob,solve(k)});
  }
  return merge_vec(items);
}

void gen_decks_rec(int i,int left,Counts& cur,std::vector<Counts>& out){
  if(i==13){
    if(left==0 && cur[VOID_REND]==1 && cur[UPPERCUT]==1 && cur[NEUTRALIZE]==1 && cur[BASH]+cur[CLOTHESLINE]==1 && cur[QUICK]+cur[BALL]+cur[SWORD]==2 && cur[COLD]+cur[DEFEND]==2) out.push_back(cur);
    return;
  }
  for(int k=0;k<=std::min(POOL[i],left);k++){ cur[i]=k; gen_decks_rec(i+1,left-k,cur,out); }
  cur[i]=0;
}
std::vector<Counts> decks(){ std::vector<Counts> out; Counts c{}; gen_decks_rec(0,9,c,out); return out; }
int first_turn(Vec v){ for(int i=0;i<4;i++) if(v.p[i]>1e-9) return i+1; return 0; }
std::string deck_str(Counts d){ std::ostringstream os; bool first=true; for(int i=0;i<12;i++) if(d[i]){ if(!first) os<<"、"; first=false; os<<CN[i]; if(d[i]>1) os<<" x"<<(int)d[i]; } return os.str(); }
std::string family(Counts d,int p,int r){
  if(r==VOID_LENS) return "虚空透镜线";
  if(r==SHURIKEN && d[SWORD]) return "手里剑多段线";
  if(p==BREAKER) return "破障易伤线";
  if(p==ENERGY_POTION) return "能量爆发线";
  if(r==ANCHOR && d[COLD]) return "寒流锚线";
  if(r==ANCHOR) return "锚防守线";
  if(p==FIRE) return "火焰补刀线";
  if(d[QUICK]+d[BALL]>=3) return "小伤堆叠线";
  return "混合线";
}

int main(int argc,char** argv){
  if(argc>=3){ ENEMY_HP=std::atoi(argv[1]); PLAYER_HP=std::atoi(argv[2]); }
  auto ds=decks(); std::vector<Row> rows; int done=0,total_builds=(int)ds.size()*9;
  for(auto d:ds) for(int p=0;p<3;p++) for(int r=0;r<3;r++){
    memo.clear(); Vec v=result_for(d,p,r);
    rows.push_back({d,p,r,first_turn(v),v,success(v),family(d,p,r),deck_str(d)+"；"+POTION_CN[p]+"；"+RELIC_CN[r]});
    done++; if(done%100==0 || done==total_builds){ std::cout<<"audited "<<done<<"/"<<total_builds<<"\n"; std::cout.flush(); }
  }
  std::sort(rows.begin(),rows.end(),[](auto&a,auto&b){ if(std::abs(a.succ-b.succ)>1e-12) return a.succ>b.succ; int af=a.ft?a.ft:99,bf=b.ft?b.ft:99; if(af!=bf) return af<bf; return a.vec.p[2]>b.vec.p[2]; });
  int perfect=0; for(auto&r:rows) if(r.succ>0.999999) perfect++;
  Row best_turn[5]; bool has_turn[5]{};
  std::vector<std::string> fam_names={"虚空透镜线","手里剑多段线","破障易伤线","能量爆发线","寒流锚线","锚防守线","火焰补刀线","小伤堆叠线","混合线"};
  std::vector<Row> best_fam(fam_names.size()); std::vector<bool> has_fam(fam_names.size(),false);
  for(auto const& r:rows){
    int t=r.ft; if(t>=0&&t<=4&&(!has_turn[t]||r.succ>best_turn[t].succ)){ best_turn[t]=r; has_turn[t]=true; }
    for(int i=0;i<(int)fam_names.size();i++) if(r.fam==fam_names[i]&&(!has_fam[i]||r.succ>best_fam[i].succ)){ best_fam[i]=r; has_fam[i]=true; }
  }
  std::ofstream jf("difficulty5_void_lock_fast_audit.json");
  jf<<"{\"enemy_hp\":"<<ENEMY_HP<<",\"player_hp\":"<<PLAYER_HP<<",\"legal_deck_count\":"<<ds.size()<<",\"legal_build_count\":"<<rows.size()<<",\"perfect_success_count\":"<<perfect<<",\"top30\":[";
  for(int i=0;i<30&&i<(int)rows.size();i++){ if(i) jf<<","; auto&r=rows[i]; jf<<"{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"]}"; }
  jf<<"],\"best_by_first_turn\":{"; bool first=true; for(int t=0;t<=4;t++) if(has_turn[t]){ if(!first) jf<<","; first=false; auto&r=best_turn[t]; jf<<"\""<<t<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"]}"; }
  jf<<"},\"best_by_family\":{"; first=true; for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ if(!first) jf<<","; first=false; auto&r=best_fam[i]; jf<<"\""<<fam_names[i]<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"]}"; }
  jf<<"}}"; jf.close();
  auto&r=rows.front();
  std::cout<<"enemy_hp "<<ENEMY_HP<<" player_hp "<<PLAYER_HP<<"\nlegal_deck_count "<<ds.size()<<"\nlegal_build_count "<<rows.size()<<"\nperfect_success_count "<<perfect<<"\n";
  std::cout<<"best "<<r.succ*100<<" first "<<r.ft<<" "<<r.fam<<" ["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"] "<<r.display<<"\n";
  std::cout<<"best_by_first_turn\n";
  for(int t=0;t<=4;t++) if(has_turn[t]){ auto&b=best_turn[t]; std::cout<<t<<" "<<b.succ*100<<" "<<b.fam<<" ["<<b.vec.p[0]*100<<","<<b.vec.p[1]*100<<","<<b.vec.p[2]*100<<","<<b.vec.p[3]*100<<","<<b.vec.p[4]*100<<"] "<<b.display<<"\n"; }
  std::cout<<"best_by_family\n";
  for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ auto&b=best_fam[i]; std::cout<<fam_names[i]<<" "<<b.succ*100<<" first "<<b.ft<<" ["<<b.vec.p[0]*100<<","<<b.vec.p[1]*100<<","<<b.vec.p[2]*100<<","<<b.vec.p[3]*100<<","<<b.vec.p[4]*100<<"] "<<b.display<<"\n"; }
}
