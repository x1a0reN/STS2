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
enum Card { BASH, UPPERCUT, NEUTRALIZE, CARNAGE, BURNING_PACT, TRUE_GRIT, SURVIVOR, DAGGER, QUICK, BALL, IRON_WAVE, STRIKE, DEFEND, BURN };
enum Potion { FIRE, CLARITY, GHOST };

const int POOL[14] = {1,1,1,1,1,0,0,1,2,2,1,3,2,0};
const int COST[14] = {2,2,0,2,1,1,1,1,1,1,1,1,1,99};
const char* CN[14] = {"重击","上勾拳","中和","残杀（改）","燃烧契约（改）","坚毅（改）","生存者（改）","投掷匕首（改）","快斩（改）","球状闪电（改）","铁斩波","打击","防御","灼伤"};
const char* POTION_CN[3] = {"火焰药水","清醒药水","幽灵药水"};
int ENEMY_HP=118, PLAYER_HP=22, DMG[5]={7,13,17,24,34}, ARMOR_GAIN[5]={0,8,0,12,0}, BURN_GAIN[5]={1,0,1,0,0};

struct Vec { double p[6]{}; };
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
struct Ctx {
  Counts hand{}, discard{};
  int hp=0, enemy=0, armor=0, vuln=0, weak=0, energy=3, block=0, potion_used=0, ghost=0;
};
struct CtxKey {
  Counts hand{}, discard{};
  int hp=0, enemy=0, armor=0, vuln=0, weak=0, energy=0, block=0, potion_used=0, ghost=0;
  bool operator==(CtxKey const& o) const {
    return hp==o.hp&&enemy==o.enemy&&armor==o.armor&&vuln==o.vuln&&weak==o.weak&&energy==o.energy&&block==o.block&&potion_used==o.potion_used&&ghost==o.ghost&&hand==o.hand&&discard==o.discard;
  }
};
struct CtxKeyHash {
  size_t operator()(CtxKey const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.hp); mix(k.enemy); mix(k.armor); mix(k.vuln); mix(k.weak); mix(k.energy); mix(k.block); mix(k.potion_used); mix(k.ghost);
    for(auto v:k.hand) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct Row { Counts deck; int potion, ft; Vec vec; double succ; std::string fam, display; };

static std::unordered_map<Key, Vec, KeyHash> memo;

int total(Counts const& c){ int s=0; for(auto v:c) s+=v; return s; }
Counts add(Counts a, Counts const& b){ for(int i=0;i<14;i++) a[i]+=b[i]; return a; }
Counts sub_one(Counts a, int card){ a[card]--; return a; }
Counts add_one(Counts a, int card){ a[card]++; return a; }
long long C(int n,int k){ if(k<0||k>n) return 0; if(k==0||k==n) return 1; long long r=1; for(int i=1;i<=k;i++) r=r*(n-k+i)/i; return r; }
int atk(int base,int vuln){ return vuln>0 ? (base*3)/2 : base; }
int weakd(int base,int weak){ return weak>0 ? (base*3)/4 : base; }
void deal(int &enemy,int &armor,int amount){ int b=std::min(armor,amount); armor-=b; enemy-=amount-b; }
double success(Vec const& v){ return v.p[0]+v.p[1]+v.p[2]+v.p[3]+v.p[4]; }
Vec fail(){ Vec v; v.p[5]=1; return v; }
Vec kill(int turn){ Vec v; v.p[turn-1]=1; return v; }
bool better(Vec const& a, Vec const& b, bool has){
  if(!has) return true;
  if(std::abs(success(a)-success(b))>1e-12) return success(a)>success(b);
  for(int i=0;i<5;i++) if(std::abs(a.p[i]-b.p[i])>1e-12) return a.p[i]>b.p[i];
  return false;
}
Vec merge_vec(std::vector<std::pair<double,Vec>> const& items){ Vec out; for(auto const& it:items) for(int i=0;i<6;i++) out.p[i]+=it.first*it.second.p[i]; return out; }

std::vector<Draw> draw_outcomes(Counts cards, int n, Counts fixed_discard={}) {
  std::vector<Draw> out; int m=total(cards);
  if(n>=m){ out.push_back({cards,{},fixed_discard,1.0}); return out; }
  long long denom=C(m,n); Counts pick{};
  std::function<void(int,int,long long)> rec = [&](int i,int left,long long ways){
    if(i==14){ if(left==0){ Counts rest{}; for(int j=0;j<14;j++) rest[j]=cards[j]-pick[j]; out.push_back({pick,rest,fixed_discard,(double)ways/(double)denom}); } return; }
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

std::vector<Ctx> end_contexts(Key const& k) {
  Ctx start; start.hand=k.hand; start.discard=k.discard; start.hp=k.hp; start.enemy=k.enemy; start.armor=k.armor; start.vuln=k.vuln; start.weak=k.weak; start.potion_used=k.potion_used;
  std::vector<Ctx> starts{start};
  if(!start.potion_used) {
    Ctx p=start; p.potion_used=1;
    if(k.potion==FIRE) deal(p.enemy,p.armor,14);
    else if(k.potion==GHOST) p.ghost=1;
    else if(k.potion==CLARITY) {
      int remove=std::min<int>(2,p.hand[BURN]);
      p.hand[BURN]-=remove;
      deal(p.enemy,p.armor,10*remove);
    }
    starts.push_back(p);
  }

  std::vector<Ctx> finals;
  for(auto const& s:starts) {
    std::vector<Counts> subsets;
    Counts cur{};
    std::function<void(int,int)> rec=[&](int idx,int spent){
      if(idx==13){
        bool can_add=false;
        for(int c=0;c<13;c++) if(cur[c]<s.hand[c] && spent+COST[c]<=3) can_add=true;
        if(!can_add) subsets.push_back(cur);
        return;
      }
      int mx=s.hand[idx];
      for(int n=0;n<=mx;n++){
        int ns=spent+n*COST[idx];
        if(ns<=3){ cur[idx]=n; rec(idx+1,ns); }
      }
      cur[idx]=0;
    };
    rec(0,0);
    for(auto sub:subsets) {
      if(sub[TRUE_GRIT] && total(s.hand)-total(sub)<=0) continue;
      Ctx base=s;
      for(int c=0;c<13;c++){ base.hand[c]-=sub[c]; base.energy-=sub[c]*COST[c]; }
      for(int i=0;i<sub[BASH];i++){ deal(base.enemy,base.armor,atk(8,base.vuln)); base.vuln+=2; base.discard=add_one(base.discard,BASH); }
      for(int i=0;i<sub[UPPERCUT];i++){ deal(base.enemy,base.armor,atk(13,base.vuln)); base.vuln+=1; base.weak+=1; base.discard=add_one(base.discard,UPPERCUT); }
      for(int i=0;i<sub[NEUTRALIZE];i++){ deal(base.enemy,base.armor,atk(3,base.vuln)); base.weak+=1; base.discard=add_one(base.discard,NEUTRALIZE); }
      for(int i=0;i<sub[CARNAGE];i++){ deal(base.enemy,base.armor,atk(18,base.vuln)); base.discard=add_one(base.discard,CARNAGE); }
      for(int i=0;i<sub[DAGGER];i++){ deal(base.enemy,base.armor,atk(9,base.vuln)); base.discard=add_one(base.discard,DAGGER); }
      for(int i=0;i<sub[QUICK];i++){ deal(base.enemy,base.armor,atk(8,base.vuln)); base.discard=add_one(base.discard,QUICK); }
      for(int i=0;i<sub[BALL];i++){ deal(base.enemy,base.armor,atk(7,base.vuln)); base.discard=add_one(base.discard,BALL); }
      for(int i=0;i<sub[IRON_WAVE];i++){ deal(base.enemy,base.armor,atk(5,base.vuln)); base.block+=5; base.discard=add_one(base.discard,IRON_WAVE); }
      for(int i=0;i<sub[STRIKE];i++){ deal(base.enemy,base.armor,atk(6,base.vuln)); base.discard=add_one(base.discard,STRIKE); }
      for(int i=0;i<sub[DEFEND];i++){ base.block+=5; base.discard=add_one(base.discard,DEFEND); }
      for(int i=0;i<sub[SURVIVOR];i++){ base.block+=7; if(base.hand[BURN]>0) base.hand[BURN]--; base.discard=add_one(base.discard,SURVIVOR); }

      std::vector<Ctx> target_states{base};
      for(int i=0;i<sub[TRUE_GRIT];i++) {
        std::vector<Ctx> next;
        for(auto st:target_states) {
          st.block+=7;
          for(int t=0;t<14;t++) if(st.hand[t]>0){ Ctx x=st; x.hand=sub_one(x.hand,t); x.discard=add_one(x.discard,TRUE_GRIT); next.push_back(x); }
        }
        target_states.swap(next);
      }
      for(int i=0;i<sub[BURNING_PACT];i++) {
        for(auto &st:target_states) {
          if(st.hand[BURN]>0) st.hand[BURN]--;
          deal(st.enemy,st.armor,atk(10,st.vuln));
          st.discard=add_one(st.discard,BURNING_PACT);
        }
      }
      finals.insert(finals.end(), target_states.begin(), target_states.end());
    }
  }
  std::sort(finals.begin(), finals.end(), [](auto const& a, auto const& b){
    if(a.enemy!=b.enemy) return a.enemy<b.enemy;
    if(a.hp!=b.hp) return a.hp>b.hp;
    return total(a.hand)<total(b.hand);
  });
  finals.erase(std::unique(finals.begin(), finals.end(), [](auto const& a, auto const& b){
    return a.hand==b.hand&&a.discard==b.discard&&a.hp==b.hp&&a.enemy==b.enemy&&a.armor==b.armor&&a.vuln==b.vuln&&a.weak==b.weak&&a.energy==b.energy&&a.block==b.block&&a.potion_used==b.potion_used&&a.ghost==b.ghost;
  }), finals.end());
  return finals;
}

Vec solve(Key k) {
  if(k.hp<=0) return fail();
  if(k.enemy<=0) return kill(k.turn);
  auto it=memo.find(k); if(it!=memo.end()) return it->second;
  Vec best; bool has=false;
  for(auto ctx:end_contexts(k)) {
    Vec cand;
    if(ctx.enemy<=0) cand=kill(k.turn);
    else {
      int hp2=ctx.hp - 3*(int)ctx.hand[BURN];
      int incoming=weakd(DMG[k.turn-1],ctx.weak);
      if(ctx.ghost) incoming/=2;
      hp2 -= std::max(0,incoming-ctx.block);
      if(k.turn>=5 || hp2<=0) cand=fail();
      else {
        Counts discard2=add(ctx.discard,ctx.hand);
        for(int i=0;i<BURN_GAIN[k.turn-1];i++) discard2=add_one(discard2,BURN);
        int armor2=ctx.armor+ARMOR_GAIN[k.turn-1];
        std::vector<std::pair<double,Vec>> items;
        for(auto d:next_draws(k.draw,discard2,5)) {
          Key nk{k.turn+1,hp2,ctx.enemy,armor2,std::max(0,ctx.vuln-1),std::max(0,ctx.weak-1),ctx.potion_used,k.potion,d.hand,d.rest,d.discard};
          items.push_back({d.prob,solve(nk)});
        }
        cand=merge_vec(items);
      }
    }
    if(better(cand,best,has)){ best=cand; has=true; }
  }
  if(!has) best=fail();
  memo.emplace(k,best);
  return best;
}

Vec result_for(Counts deck,int potion) {
  std::vector<std::pair<double,Vec>> items;
  for(auto d:draw_outcomes(deck,5)) {
    Key k{1,PLAYER_HP,ENEMY_HP,0,0,0,0,potion,d.hand,d.rest,{}};
    items.push_back({d.prob,solve(k)});
  }
  return merge_vec(items);
}

void gen_decks_rec(int i,int left,Counts& cur,std::vector<Counts>& out){
  if(i==14){
    if(left==0 && cur[BURNING_PACT]==1 && cur[CARNAGE]==1 && cur[NEUTRALIZE]+cur[IRON_WAVE]==1 && cur[QUICK]+cur[BALL]==2) out.push_back(cur);
    return;
  }
  for(int k=0;k<=std::min(POOL[i],left);k++){ cur[i]=k; gen_decks_rec(i+1,left-k,cur,out); }
  cur[i]=0;
}
std::vector<Counts> decks(){ std::vector<Counts> out; Counts c{}; gen_decks_rec(0,9,c,out); return out; }
int first_turn(Vec v){ for(int i=0;i<5;i++) if(v.p[i]>1e-9) return i+1; return 0; }
std::string deck_str(Counts d){ std::ostringstream os; bool first=true; for(int i=0;i<13;i++) if(d[i]){ if(!first) os<<"、"; first=false; os<<CN[i]; if(d[i]>1) os<<" x"<<(int)d[i]; } return os.str(); }
std::string family(Counts d,int p){
  if(p==FIRE && d[CARNAGE] && (d[BASH]||d[UPPERCUT])) return "火焰易伤快线";
  if(p==GHOST) return "幽灵拖回合线";
  if(p==CLARITY || d[BURNING_PACT] || d[TRUE_GRIT] || d[SURVIVOR]) return "清理灼伤线";
  if(d[QUICK]>=2 && d[BALL]>=2) return "小伤堆叠线";
  return "混合线";
}

int main(int argc,char** argv){
  if(argc>=8){
    ENEMY_HP=std::atoi(argv[1]); PLAYER_HP=std::atoi(argv[2]);
    for(int i=0;i<5;i++) DMG[i]=std::atoi(argv[3+i]);
  }
  auto ds=decks(); std::vector<Row> rows;
  int done=0, total_builds=(int)ds.size()*3;
  for(auto d:ds) for(int p=0;p<3;p++){
    memo.clear();
    Vec v=result_for(d,p);
    rows.push_back({d,p,first_turn(v),v,success(v),family(d,p),deck_str(d)+"；"+POTION_CN[p]});
    done++;
    if(done%100==0 || done==total_builds){ std::cout<<"audited "<<done<<"/"<<total_builds<<"\n"; std::cout.flush(); }
  }
  std::sort(rows.begin(),rows.end(),[](auto&a,auto&b){ if(std::abs(a.succ-b.succ)>1e-12) return a.succ>b.succ; int af=a.ft?a.ft:99,bf=b.ft?b.ft:99; if(af!=bf) return af<bf; return a.vec.p[2]>b.vec.p[2]; });
  int perfect=0; for(auto&r:rows) if(r.succ>0.999999) perfect++;
  Row best_turn[6]; bool has_turn[6]{};
  std::vector<std::string> fam_names={"火焰易伤快线","清理灼伤线","幽灵拖回合线","小伤堆叠线","混合线"};
  std::vector<Row> best_fam(fam_names.size()); std::vector<bool> has_fam(fam_names.size(),false);
  for(auto const& r:rows){
    int t=r.ft; if(t>=0&&t<=5&&(!has_turn[t]||r.succ>best_turn[t].succ)){ best_turn[t]=r; has_turn[t]=true; }
    for(int i=0;i<(int)fam_names.size();i++) if(r.fam==fam_names[i]&&(!has_fam[i]||r.succ>best_fam[i].succ)){ best_fam[i]=r; has_fam[i]=true; }
  }
  std::ofstream jf("difficulty4_burn_countdown_audit.json");
  jf<<"{\"enemy_hp\":"<<ENEMY_HP<<",\"player_hp\":"<<PLAYER_HP<<",\"legal_deck_count\":"<<ds.size()<<",\"legal_build_count\":"<<rows.size()<<",\"perfect_success_count\":"<<perfect<<",\"top30\":[";
  for(int i=0;i<30&&i<(int)rows.size();i++){ if(i) jf<<","; auto&r=rows[i]; jf<<"{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"]}"; }
  jf<<"],\"best_by_first_turn\":{"; bool first=true; for(int t=0;t<=5;t++) if(has_turn[t]){ if(!first) jf<<","; first=false; auto&r=best_turn[t]; jf<<"\""<<t<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"family\":\""<<r.fam<<"\",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"]}"; }
  jf<<"},\"best_by_family\":{"; first=true; for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ if(!first) jf<<","; first=false; auto&r=best_fam[i]; jf<<"\""<<fam_names[i]<<"\":{\"success\":"<<r.succ*100<<",\"first_turn\":"<<r.ft<<",\"build_display\":\""<<r.display<<"\",\"kill_vector\":["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"]}"; }
  jf<<"}}"; jf.close();
  auto&r=rows.front();
  std::cout<<"enemy_hp "<<ENEMY_HP<<" player_hp "<<PLAYER_HP<<"\nlegal_deck_count "<<ds.size()<<"\nlegal_build_count "<<rows.size()<<"\nperfect_success_count "<<perfect<<"\n";
  std::cout<<"best "<<r.succ*100<<" first "<<r.ft<<" "<<r.fam<<" ["<<r.vec.p[0]*100<<","<<r.vec.p[1]*100<<","<<r.vec.p[2]*100<<","<<r.vec.p[3]*100<<","<<r.vec.p[4]*100<<","<<r.vec.p[5]*100<<"] "<<r.display<<"\n";
  std::cout<<"best_by_first_turn\n";
  for(int t=0;t<=5;t++) if(has_turn[t]){ auto&b=best_turn[t]; std::cout<<t<<" "<<b.succ*100<<" "<<b.fam<<" ["<<b.vec.p[0]*100<<","<<b.vec.p[1]*100<<","<<b.vec.p[2]*100<<","<<b.vec.p[3]*100<<","<<b.vec.p[4]*100<<","<<b.vec.p[5]*100<<"] "<<b.display<<"\n"; }
  std::cout<<"best_by_family\n";
  for(int i=0;i<(int)fam_names.size();i++) if(has_fam[i]){ auto&b=best_fam[i]; std::cout<<fam_names[i]<<" "<<b.succ*100<<" first "<<b.ft<<" ["<<b.vec.p[0]*100<<","<<b.vec.p[1]*100<<","<<b.vec.p[2]*100<<","<<b.vec.p[3]*100<<","<<b.vec.p[4]*100<<","<<b.vec.p[5]*100<<"] "<<b.display<<"\n"; }
}
