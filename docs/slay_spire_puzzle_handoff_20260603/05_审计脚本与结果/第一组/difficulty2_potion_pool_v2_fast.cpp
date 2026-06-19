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

using Counts = std::array<unsigned char, 10>;
enum Card { BASH, CLOTHESLINE, NEUTRALIZE, BALL, DAGGER, QUICK, STRIKE, SURVIVOR, DEFEND, STEAM };
enum Potion { FIRE, VULN, WEAK };

const int POOL[10] = {1,1,1,2,1,2,3,1,2,0};
const int COST[10] = {2,2,0,1,1,1,1,1,1,0};
const char* CN[10] = {"重击","金刚臂","中和","球状闪电（改）","投掷匕首（改）","快斩（改）","打击","生存者","防御","蒸汽屏障"};
const char* POTION_CN[3] = {"火焰药水","破甲药水","虚弱药水"};
int ENEMY_HP=99, PLAYER_HP=15, DMG[4]={2,10,18,24}, ARMOR_GAIN[4]={14,0,0,0}, HEAL[4]={0,0,0,0};

struct Vec { double p[5]{}; };
struct Draw { Counts hand{}, rest{}, discard{}; double prob{}; };
struct Key {
  int turn,hp,enemy,armor,vuln,weak,potion_used,potion;
  Counts hand,draw,discard;
  bool operator==(Key const& o) const {
    return turn==o.turn&&hp==o.hp&&enemy==o.enemy&&armor==o.armor&&vuln==o.vuln&&weak==o.weak&&potion_used==o.potion_used&&potion==o.potion&&hand==o.hand&&draw==o.draw&&discard==o.discard;
  }
};
struct KeyHash {
  size_t operator()(Key const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.turn); mix(k.hp); mix(k.enemy); mix(k.armor); mix(k.vuln); mix(k.weak); mix(k.potion_used); mix(k.potion);
    for(auto v:k.hand) mix(v); for(auto v:k.draw) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct Row { Counts deck; int potion, ft; Vec vec; double succ; std::string fam, display; };
static std::unordered_map<Key, Vec, KeyHash> memo;

int total(Counts const& c){ int s=0; for(auto v:c) s+=v; return s; }
Counts add(Counts a, Counts const& b){ for(int i=0;i<10;i++) a[i]+=b[i]; return a; }
Counts sub_one(Counts a, int card){ a[card]--; return a; }
Counts add_one(Counts a, int card){ a[card]++; return a; }
long long C(int n,int k){ if(k<0||k>n) return 0; if(k==0||k==n) return 1; long long r=1; for(int i=1;i<=k;i++) r=r*(n-k+i)/i; return r; }
int atk(int base,int vuln){ return vuln>0 ? (base*3)/2 : base; }
int weakd(int base,int weak){ return weak>0 ? (base*3)/4 : base; }
void deal(int &enemy,int &armor,int amount){ int b=std::min(armor,amount); armor-=b; enemy-=amount-b; }
double success(Vec const& v){ return v.p[0]+v.p[1]+v.p[2]+v.p[3]; }
Vec fail(){ Vec v; v.p[4]=1; return v; }
Vec kill(int turn){ Vec v; v.p[turn-1]=1; return v; }
bool better(Vec const& a, Vec const& b, bool has){ if(!has) return true; if(std::abs(success(a)-success(b))>1e-12) return success(a)>success(b); for(int i=0;i<4;i++) if(std::abs(a.p[i]-b.p[i])>1e-12) return a.p[i]>b.p[i]; return false; }
Vec merge_vec(std::vector<std::pair<double,Vec>> const& items){ Vec out; for(auto const& it:items) for(int i=0;i<5;i++) out.p[i]+=it.first*it.second.p[i]; return out; }

std::vector<Draw> draw_outcomes(Counts cards, int n, Counts fixed_discard={}) {
  std::vector<Draw> out; int m=total(cards);
  if(n>=m){ out.push_back({cards,{},fixed_discard,1.0}); return out; }
  long long denom=C(m,n); Counts pick{};
  std::function<void(int,int,long long)> rec = [&](int i,int left,long long ways){
    if(i==10){ if(left==0){ Counts rest{}; for(int j=0;j<10;j++) rest[j]=cards[j]-pick[j]; out.push_back({pick,rest,fixed_discard,(double)ways/(double)denom}); } return; }
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

void gen_subsets(Counts hand,int energy,std::vector<Counts>& out){
  Counts cur{};
  std::function<void(int,int)> rec=[&](int i,int spent){
    if(i==10){
      bool can_add=false;
      for(int c=0;c<10;c++) if(cur[c]<hand[c] && spent+COST[c]<=energy) can_add=true;
      if(!can_add) out.push_back(cur);
      return;
    }
    int mx=hand[i];
    for(int k=0;k<=mx;k++){ int ns=spent+k*COST[i]; if(ns<=energy){ cur[i]=k; rec(i+1,ns); } }
    cur[i]=0;
  };
  rec(0,0);
}

void resolve_subset(Counts sub, int &enemy, int &armor, int &vuln, int &weak, int &block){
  for(int c=0;c<10;c++) for(int k=0;k<sub[c];k++){
    switch(c){
      case BASH: deal(enemy,armor,atk(8,vuln)); vuln+=2; break;
      case CLOTHESLINE: deal(enemy,armor,atk(12,vuln)); weak+=2; break;
      case NEUTRALIZE: deal(enemy,armor,atk(3,vuln)); weak+=1; break;
      case BALL: deal(enemy,armor,atk(7,vuln)); break;
      case DAGGER: deal(enemy,armor,atk(9,vuln)); break;
      case QUICK: deal(enemy,armor,atk(8,vuln)); break;
      case STRIKE: deal(enemy,armor,atk(6,vuln)); break;
      case SURVIVOR: block+=8; break;
      case DEFEND: block+=5; break;
      case STEAM: block+=6; break;
    }
  }
}

Vec solve(Key k){
  if(k.hp<=0) return fail();
  if(k.enemy<=0) return kill(k.turn);
  auto it=memo.find(k); if(it!=memo.end()) return it->second;
  Vec best; bool has=false;
  std::vector<Key> starts{k};
  if(!k.potion_used){
    Key x=k; x.potion_used=1;
    if(k.potion==FIRE) deal(x.enemy,x.armor,20);
    else if(k.potion==VULN) x.vuln+=2;
    else if(k.potion==WEAK) x.weak+=2;
    starts.push_back(x);
  }
  for(auto s:starts){
    std::vector<Counts> subs; gen_subsets(s.hand,3,subs);
    for(auto sub:subs){
      int enemy=s.enemy, armor=s.armor, vuln=s.vuln, weak=s.weak, block=0;
      resolve_subset(sub,enemy,armor,vuln,weak,block);
      Vec cand;
      if(enemy<=0) cand=kill(s.turn);
      else {
        int incoming=weakd(DMG[s.turn-1],weak);
        int hp2=s.hp-std::max(0,incoming-block);
        int enemy2=std::min(ENEMY_HP, enemy+HEAL[s.turn-1]);
        int armor2=armor+ARMOR_GAIN[s.turn-1];
        if(s.turn>=4 || hp2<=0) cand=fail();
        else {
          Counts hand_left=s.hand;
          for(int c=0;c<10;c++) hand_left[c]-=sub[c];
          Counts discard2=add(s.discard,hand_left);
          discard2=add(discard2,sub);
          std::vector<std::pair<double,Vec>> items;
          for(auto d:next_draws(s.draw,discard2,5)){
            Key nk{s.turn+1,hp2,enemy2,armor2,std::max(0,vuln-1),std::max(0,weak-1),s.potion_used,s.potion,d.hand,d.rest,d.discard};
            items.push_back({d.prob,solve(nk)});
          }
          cand=merge_vec(items);
        }
      }
      if(better(cand,best,has)){ best=cand; has=true; }
    }
  }
  if(!has) best=fail();
  memo.emplace(k,best);
  return best;
}

Vec result_for(Counts deck,int potion){
  std::vector<std::pair<double,Vec>> items;
  for(auto d:draw_outcomes(deck,5)){
    Key k{1,PLAYER_HP,ENEMY_HP,0,0,0,0,potion,d.hand,d.rest,{}};
    items.push_back({d.prob,solve(k)});
  }
  return merge_vec(items);
}

void gen_decks_rec(int i,int left,Counts& cur,std::vector<Counts>& out){
  if(i==10){ if(left==0) out.push_back(cur); return; }
  for(int k=0;k<=std::min(POOL[i],left);k++){ cur[i]=k; gen_decks_rec(i+1,left-k,cur,out); }
  cur[i]=0;
}
std::vector<Counts> decks(){ std::vector<Counts> out; Counts c{}; gen_decks_rec(0,6,c,out); return out; }
int first_turn(Vec v){ for(int i=0;i<4;i++) if(v.p[i]>1e-9) return i+1; return 0; }
std::string deck_str(Counts d){ std::ostringstream os; bool first=true; for(int i=0;i<10;i++) if(d[i]){ if(!first) os<<"、"; first=false; os<<CN[i]; if(d[i]>1) os<<" x"<<(int)d[i]; } return os.str(); }
std::string family(Counts d,int p){
  if(p==FIRE && d[BASH] && d[QUICK]>=2) return "火焰快杀线";
  if(p==VULN && d[CLOTHESLINE] && d[DAGGER]) return "破甲压血线";
  if(p==WEAK && d[SURVIVOR] && d[NEUTRALIZE]) return "虚弱生存线";
  if(d[DEFEND]>=2 && d[SURVIVOR]) return "过量防守陷阱";
  return "混合线";
}

int main(int argc,char** argv){
  if(argc>=10){
    ENEMY_HP=std::atoi(argv[1]); PLAYER_HP=std::atoi(argv[2]);
    for(int i=0;i<4;i++) DMG[i]=std::atoi(argv[3+i]);
    for(int i=0;i<4;i++) ARMOR_GAIN[i]=std::atoi(argv[7+i]);
  }
  auto ds=decks(); std::vector<Row> rows;
  for(auto d:ds) for(int p=0;p<3;p++){ memo.clear(); Vec v=result_for(d,p); rows.push_back({d,p,first_turn(v),v,success(v),family(d,p),deck_str(d)+"；"+POTION_CN[p]}); }
  std::sort(rows.begin(),rows.end(),[](auto&a,auto&b){ if(std::abs(a.succ-b.succ)>1e-12) return a.succ>b.succ; int af=a.ft?a.ft:99,bf=b.ft?b.ft:99; if(af!=bf) return af<bf; return a.vec.p[1]>b.vec.p[1]; });
  int perfect=0; for(auto&r:rows) if(r.succ>0.999999) perfect++;
  Row best_turn[5]; bool has_turn[5]{};
  std::vector<std::string> fam_names={"火焰快杀线","破甲压血线","虚弱生存线","过量防守陷阱","混合线"};
  std::vector<Row> best_fam(fam_names.size()); std::vector<bool> has_fam(fam_names.size(),false);
  for(auto const& r:rows){
    int t=r.ft; if(t>=0&&t<=4&&(!has_turn[t]||r.succ>best_turn[t].succ)){ best_turn[t]=r; has_turn[t]=true; }
    for(int i=0;i<(int)fam_names.size();i++) if(r.fam==fam_names[i]&&(!has_fam[i]||r.succ>best_fam[i].succ)){ best_fam[i]=r; has_fam[i]=true; }
  }
  std::ofstream jf("difficulty2_potion_pool_v2_fast_audit.json");
  jf<<"{\"enemy_hp\":"<<ENEMY_HP<<",\"player_hp\":"<<PLAYER_HP<<",\"enemy_damage_by_turn\":["<<DMG[0]<<","<<DMG[1]<<","<<DMG[2]<<","<<DMG[3]<<"],\"enemy_armor_gain_by_turn\":["<<ARMOR_GAIN[0]<<","<<ARMOR_GAIN[1]<<","<<ARMOR_GAIN[2]<<","<<ARMOR_GAIN[3]<<"],\"legal_deck_count\":"<<ds.size()<<",\"legal_build_count\":"<<rows.size()<<",\"perfect_success_count\":"<<perfect<<",\"top20\":[";
  for(int i=0;i<20&&i<(int)rows.size();i++){ if(i) jf<<","; auto&r=rows[i]; jf<<"{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"]}"; }
  jf<<"],\"best_by_first_turn\":{"; bool first=true; for(int t=0;t<=4;t++) if(has_turn[t]){ if(!first) jf<<","; first=false; auto&r=best_turn[t]; jf<<"\""<<t<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"]}"; }
  jf<<"},\"best_by_family\":{"; first=true; for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ if(!first) jf<<","; first=false; auto&r=best_fam[i]; jf<<"\""<<fam_names[i]<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"]}"; }
  jf<<"}}"; jf.close();
  auto&r=rows.front();
  std::cout<<"enemy_hp "<<ENEMY_HP<<" player_hp "<<PLAYER_HP<<" damage ["<<DMG[0]<<","<<DMG[1]<<","<<DMG[2]<<","<<DMG[3]<<"] armor ["<<ARMOR_GAIN[0]<<","<<ARMOR_GAIN[1]<<","<<ARMOR_GAIN[2]<<","<<ARMOR_GAIN[3]<<"]\n";
  std::cout<<"legal_deck_count "<<ds.size()<<"\nlegal_build_count "<<rows.size()<<"\nperfect_success_count "<<perfect<<"\n";
  std::cout<<"best "<<r.succ*100<<" first "<<r.ft<<" "<<r.fam<<" ["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<"] "<<r.display<<"\n";
  std::cout<<"best_by_first_turn\n";
  for(int t=0;t<=4;t++) if(has_turn[t]){ auto&b=best_turn[t]; std::cout<<t<<" "<<b.succ*100<<" "<<b.fam<<" ["<<b.vec.p[0]*100<<","<<b.vec.p[1]*100<<","<<b.vec.p[2]*100<<","<<b.vec.p[3]*100<<","<<b.vec.p[4]*100<<"] "<<b.display<<"\n"; }
  std::cout<<"best_by_family\n";
  for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ auto&b=best_fam[i]; std::cout<<fam_names[i]<<" "<<b.succ*100<<" first "<<b.ft<<" ["<<b.vec.p[0]*100<<","<<b.vec.p[1]*100<<","<<b.vec.p[2]*100<<","<<b.vec.p[3]*100<<","<<b.vec.p[4]*100<<"] "<<b.display<<"\n"; }
}
