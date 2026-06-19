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
using Orbs = std::array<unsigned char, 3>;
using OrbVals = std::array<unsigned char, 3>;
enum Card { ZAP, DUALCAST, DARKNESS, RECURSION, LOOP, CHILL, COLD_SNAP, BALL, MELTER, STREAMLINE, STRIKE, DEFEND, LEAP, COOLHEADED };
enum Potion { FOCUS_POTION, DARK_POTION, RELEASE_POTION };
enum Relic { DATA_DISK, GOLD_CABLES, DARK_CORE };

const int POOL[14] = {2,1,2,1,1,1,2,2,1,1,3,3,2,2};
const int COST[14] = {1,1,1,1,1,0,1,1,1,2,1,1,1,1};
const char* CN[14] = {"电击（改）","双重释放（改）","黑暗（改）","递归（改）","循环（改）","寒意（改）","寒流（改）","球状闪电（改）","熔化（改）","精简改良（改）","打击","防御","飞跃","冷静头脑（改）"};
const char* POTION_CN[3] = {"集中药水","黑暗药水","释放药水"};
const char* RELIC_CN[3] = {"数据磁盘","镀金线缆","暗核"};

int ENEMY_HP=75, PLAYER_HP=40;
int DMG[5]={13,22,31,40,55};
int ARMOR_GAIN[5]={0,24,0,34,0};

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
  int turn,hp,enemy,armor,focus,potion_used,potion,relic,loop;
  Orbs orbs{}; OrbVals vals{};
  Counts hand,draw,discard;
  bool operator==(Key const& o) const {
    return turn==o.turn&&hp==o.hp&&enemy==o.enemy&&armor==o.armor&&focus==o.focus&&potion_used==o.potion_used&&potion==o.potion&&relic==o.relic&&loop==o.loop&&orbs==o.orbs&&vals==o.vals&&hand==o.hand&&draw==o.draw&&discard==o.discard;
  }
};
struct KeyHash {
  size_t operator()(Key const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.turn); mix(k.hp); mix(k.enemy); mix(k.armor); mix(k.focus); mix(k.potion_used); mix(k.potion); mix(k.relic); mix(k.loop);
    for(auto v:k.orbs) mix(v); for(auto v:k.vals) mix(v);
    for(auto v:k.hand) mix(v); for(auto v:k.draw) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct Ctx {
  Counts hand{}, discard{};
  Orbs orbs{}; OrbVals vals{};
  int enemy=0, armor=0, focus=0, potion_used=0, relic=0, loop=0, energy=3, block=0, plays=0;
};
struct CtxKey {
  Counts hand{}, discard{};
  Orbs orbs{}; OrbVals vals{};
  int enemy=0,armor=0,focus=0,potion_used=0,relic=0,loop=0,energy=0,block=0,plays=0;
  bool operator==(CtxKey const& o) const {
    return enemy==o.enemy&&armor==o.armor&&focus==o.focus&&potion_used==o.potion_used&&relic==o.relic&&loop==o.loop&&energy==o.energy&&block==o.block&&plays==o.plays&&orbs==o.orbs&&vals==o.vals&&hand==o.hand&&discard==o.discard;
  }
};
struct CtxKeyHash {
  size_t operator()(CtxKey const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.enemy); mix(k.armor); mix(k.focus); mix(k.potion_used); mix(k.relic); mix(k.loop); mix(k.energy); mix(k.block); mix(k.plays);
    for(auto v:k.orbs) mix(v); for(auto v:k.vals) mix(v);
    for(auto v:k.hand) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct CtxShape {
  Counts hand{}, discard{};
  Orbs orbs{}; OrbVals vals{};
  int focus=0,potion_used=0,relic=0,loop=0,energy=0,plays=0;
  bool operator==(CtxShape const& o) const {
    return focus==o.focus&&potion_used==o.potion_used&&relic==o.relic&&loop==o.loop&&energy==o.energy&&plays==o.plays&&orbs==o.orbs&&vals==o.vals&&hand==o.hand&&discard==o.discard;
  }
};
struct CtxShapeHash {
  size_t operator()(CtxShape const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.focus); mix(k.potion_used); mix(k.relic); mix(k.loop); mix(k.energy); mix(k.plays);
    for(auto v:k.orbs) mix(v); for(auto v:k.vals) mix(v);
    for(auto v:k.hand) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct DomEntry { int enemy=0, armor=0, block=0; Vec vec; };
struct StopKey {
  int turn=0,hp=0,enemy=0,armor=0,focus=0,potion_used=0,potion=0,relic=0,loop=0,block=0;
  Orbs orbs{}; OrbVals vals{};
  Counts draw{}, discard{};
  bool operator==(StopKey const& o) const {
    return turn==o.turn&&hp==o.hp&&enemy==o.enemy&&armor==o.armor&&focus==o.focus&&potion_used==o.potion_used&&
      potion==o.potion&&relic==o.relic&&loop==o.loop&&block==o.block&&orbs==o.orbs&&vals==o.vals&&draw==o.draw&&discard==o.discard;
  }
};
struct StopKeyHash {
  size_t operator()(StopKey const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.turn); mix(k.hp); mix(k.enemy); mix(k.armor); mix(k.focus); mix(k.potion_used); mix(k.potion); mix(k.relic); mix(k.loop); mix(k.block);
    for(auto v:k.orbs) mix(v); for(auto v:k.vals) mix(v);
    for(auto v:k.draw) mix(v); for(auto v:k.discard) mix(v);
    return h;
  }
};
struct TermKey {
  Counts hand{};
  Orbs orbs{}; OrbVals vals{};
  int enemy=0,armor=0,focus=0,potion_used=0,potion=0,relic=0,loop=0,energy=0,plays=0;
  bool operator==(TermKey const& o) const {
    return enemy==o.enemy&&armor==o.armor&&focus==o.focus&&potion_used==o.potion_used&&potion==o.potion&&
      relic==o.relic&&loop==o.loop&&energy==o.energy&&plays==o.plays&&orbs==o.orbs&&vals==o.vals&&hand==o.hand;
  }
};
struct TermKeyHash {
  size_t operator()(TermKey const& k) const {
    size_t h=1469598103934665603ull;
    auto mix=[&](int v){ h^=(unsigned long long)(v+257); h*=1099511628211ull; };
    mix(k.enemy); mix(k.armor); mix(k.focus); mix(k.potion_used); mix(k.potion); mix(k.relic); mix(k.loop); mix(k.energy); mix(k.plays);
    for(auto v:k.orbs) mix(v); for(auto v:k.vals) mix(v); for(auto v:k.hand) mix(v);
    return h;
  }
};
struct Row { Counts deck; int potion,relic,ft; Vec vec; double succ; std::string fam, display; };
static std::unordered_map<Key, Vec, KeyHash> memo;
static std::unordered_map<TermKey, bool, TermKeyHash> terminal_memo;
static std::unordered_map<StopKey, Vec, StopKeyHash> stop_memo;
Vec solve(Key k);
static long long solve_calls=0, action_calls=0;
static int trace_every=0;

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

void deal(int &enemy,int &armor,int amount){ int b=std::min(armor,amount); armor-=b; enemy-=amount-b; }
int orb_dmg(int base,int focus){ return std::max(0,base+focus); }

void evoke_front(Ctx &c){
  int t=c.orbs[0], v=c.vals[0];
  if(t==1) deal(c.enemy,c.armor,orb_dmg(8,c.focus));
  else if(t==2){ int b=orb_dmg(7,c.focus); c.block += b; c.enemy -= b; }
  else if(t==3) deal(c.enemy,c.armor,v);
  for(int i=0;i<2;i++){ c.orbs[i]=c.orbs[i+1]; c.vals[i]=c.vals[i+1]; }
  c.orbs[2]=0; c.vals[2]=0;
}

void channel(Ctx &c,int t,int value){
  if(c.orbs[2]!=0) evoke_front(c);
  int pos=0; while(pos<3 && c.orbs[pos]!=0) pos++;
  if(pos>=3) pos=2;
  c.orbs[pos]=t; c.vals[pos]=value;
}

void passive_one(Ctx &c,int idx){
  int t=c.orbs[idx];
  if(t==1) deal(c.enemy,c.armor,orb_dmg(3,c.focus));
  else if(t==2){ int b=orb_dmg(2,c.focus); c.block += b; c.enemy -= b; }
  else if(t==3) c.vals[idx]=std::min<int>(80,c.vals[idx]+orb_dmg(6,c.focus)+(c.relic==DARK_CORE?2:0));
}

std::vector<Draw> draw_outcomes(Counts cards,int n,Counts fixed_discard={}) {
  DrawKey key{cards,fixed_discard,n};
  auto memo_it=draw_memo.find(key);
  if(memo_it!=draw_memo.end()) return memo_it->second;
  std::vector<Draw> out; int m=total(cards);
  if(n>=m){ out.push_back({cards,{},fixed_discard,1.0}); draw_memo.emplace(key,out); return out; }
  long long denom=C(m,n); Counts pick{};
  std::function<void(int,int,long long)> rec=[&](int i,int left,long long ways){
    if(i==14){ if(left==0){ Counts rest{}; for(int j=0;j<14;j++) rest[j]=cards[j]-pick[j]; out.push_back({pick,rest,fixed_discard,(double)ways/(double)denom}); } return; }
    int mx=std::min<int>(cards[i],left);
    for(int k=0;k<=mx;k++){ pick[i]=k; rec(i+1,left-k,ways*C(cards[i],k)); }
    pick[i]=0;
  };
  rec(0,n,1); draw_memo.emplace(key,out); return out;
}

std::vector<Draw> next_draws(Counts draw,Counts discard,int n) {
  if(total(draw)>=n) return draw_outcomes(draw,n,discard);
  Counts fixed=draw; int need=n-total(draw);
  if(total(discard)==0) return {{fixed,{}, {},1.0}};
  auto tmp=draw_outcomes(discard,need,{});
  for(auto &d:tmp) d.hand=add(fixed,d.hand);
  return tmp;
}

bool play_card(Ctx const& in,int card,Ctx &out) {
  if(in.hand[card]==0 || in.energy<COST[card] || in.plays>=2) return false;
  out=in; out.hand=sub_one(out.hand,card); out.energy-=COST[card]; out.plays++;
  bool exhaust=false;
  switch(card){
    case ZAP: { bool empty=out.orbs[0]==0&&out.orbs[1]==0&&out.orbs[2]==0; channel(out,1,0); if(empty) channel(out,1,0); break; }
    case DUALCAST: if(out.orbs[0]!=0){ evoke_front(out); if(out.orbs[0]!=0) evoke_front(out); } break;
    case DARKNESS: channel(out,3,out.relic==DARK_CORE?10:6); break;
    case RECURSION: if(out.orbs[0]!=0){ int t=out.orbs[0], v=out.vals[0]; evoke_front(out); channel(out,t,v); } break;
    case LOOP: out.loop=1; exhaust=true; break;
    case CHILL: channel(out,2,0); break;
    case COLD_SNAP: deal(out.enemy,out.armor,6); channel(out,2,0); break;
    case BALL: deal(out.enemy,out.armor,7); channel(out,1,0); break;
    case MELTER: out.enemy-=10; break;
    case STREAMLINE: deal(out.enemy,out.armor,15); break;
    case STRIKE: deal(out.enemy,out.armor,6); break;
    case DEFEND: out.block+=5; break;
    case LEAP: out.block+=9; break;
    case COOLHEADED: out.block+=5; channel(out,2,0); break;
  }
  if(!exhaust) out.discard=add_one(out.discard,card);
  return true;
}

bool use_potion(Ctx const& in,int potion,Ctx &out) {
  if(in.potion_used) return false;
  out=in; out.potion_used=1;
  if(potion==FOCUS_POTION) out.focus+=2;
  else if(potion==DARK_POTION) channel(out,3,out.relic==DARK_CORE?18:14);
  else if(potion==RELEASE_POTION){ if(out.orbs[0]!=0) evoke_front(out); if(out.orbs[0]!=0) evoke_front(out); }
  return true;
}

std::vector<Ctx> end_contexts(Key const& k){
  Ctx start; start.hand=k.hand; start.discard=k.discard; start.orbs=k.orbs; start.vals=k.vals; start.enemy=k.enemy; start.armor=k.armor; start.focus=k.focus; start.potion_used=k.potion_used; start.relic=k.relic; start.loop=k.loop;
  std::vector<Ctx> uniq; std::unordered_set<CtxKey, CtxKeyHash> final_seen, action_seen;
  auto key_for=[&](Ctx c){
    if(c.enemy<0) c.enemy=0;
    c.block=std::min(c.block,DMG[k.turn-1]);
    return CtxKey{c.hand,c.discard,c.orbs,c.vals,c.enemy,c.armor,c.focus,c.potion_used,c.relic,c.loop,c.energy,c.block,c.plays};
  };
  auto push_final=[&](Ctx const& c){
    Ctx normalized=c;
    normalized.discard=add(c.discard,c.hand);
    normalized.hand={};
    normalized.energy=0;
    normalized.plays=0;
    CtxKey ck=key_for(normalized);
    if(final_seen.insert(ck).second) uniq.push_back(normalized);
  };
  std::function<void(Ctx const&)> bestFromCtx=[&](Ctx const& c){
    CtxKey ak=key_for(c);
    if(!action_seen.insert(ak).second) return;
    push_final(c);
    if(c.enemy<=0) return;
    if(!c.potion_used){
      Ctx out;
      if(use_potion(c,k.potion,out)) bestFromCtx(out);
    }
    if(c.plays>=2) return;
    for(int card=0;card<14;card++){
      Ctx out;
      if(play_card(c,card,out)) bestFromCtx(out);
    }
  };
  bestFromCtx(start);
  return uniq;
}

CtxKey action_key_for(Key const& k, Ctx c){
  if(c.enemy<0) c.enemy=0;
  c.block=std::min(c.block,DMG[k.turn-1]);
  if(k.turn>=5) c.discard={};
  return CtxKey{c.hand,c.discard,c.orbs,c.vals,c.enemy,c.armor,c.focus,c.potion_used,c.relic,c.loop,c.energy,c.block,c.plays};
}

CtxShape action_shape_for(Key const& k, Ctx c){
  if(k.turn>=5) c.discard={};
  return CtxShape{c.hand,c.discard,c.orbs,c.vals,c.focus,c.potion_used,c.relic,c.loop,c.energy,c.plays};
}

bool terminal_can_kill(Ctx c,int potion){
  if(c.enemy<=0) return true;
  TermKey tk{c.hand,c.orbs,c.vals,std::max(0,c.enemy),c.armor,c.focus,c.potion_used,potion,c.relic,c.loop,c.energy,c.plays};
  auto it=terminal_memo.find(tk);
  if(it!=terminal_memo.end()) return it->second;
  Ctx stopped=c;
  for(int i=0;i<3;i++) if(stopped.orbs[i]) passive_one(stopped,i);
  if(stopped.loop && stopped.orbs[0]) passive_one(stopped,0);
  if(stopped.relic==GOLD_CABLES && stopped.orbs[0]) passive_one(stopped,0);
  if(stopped.enemy<=0){ terminal_memo.emplace(tk,true); return true; }
  if(!c.potion_used){
    Ctx out;
    if(use_potion(c,potion,out) && terminal_can_kill(out,potion)){ terminal_memo.emplace(tk,true); return true; }
  }
  if(c.plays<2){
    for(int card=0;card<14;card++){
      Ctx out;
      if(play_card(c,card,out) && terminal_can_kill(out,potion)){ terminal_memo.emplace(tk,true); return true; }
    }
  }
  terminal_memo.emplace(tk,false);
  return false;
}

Key canonical_key(Key k){
  if(k.turn>=5 && k.hp>0 && k.enemy>0){
    k.hp=1;
    k.draw={};
    k.discard={};
  }
  return k;
}

Vec resolve_stopped(Key const& k, Ctx c){
  c.discard=add(c.discard,c.hand);
  c.hand={};
  if(c.enemy<=0) return kill(k.turn);
  StopKey sk{k.turn,k.hp,std::max(0,c.enemy),c.armor,c.focus,c.potion_used,k.potion,k.relic,c.loop,std::min(c.block,DMG[k.turn-1]),c.orbs,c.vals,k.draw,c.discard};
  auto stop_it=stop_memo.find(sk);
  if(stop_it!=stop_memo.end()) return stop_it->second;
  for(int i=0;i<3;i++) if(c.orbs[i]) passive_one(c,i);
  if(c.loop && c.orbs[0]) passive_one(c,0);
  if(c.relic==GOLD_CABLES && c.orbs[0]) passive_one(c,0);
  if(c.enemy<=0){ Vec v=kill(k.turn); stop_memo.emplace(sk,v); return v; }
  int hp2=k.hp-std::max(0,DMG[k.turn-1]-c.block);
  int armor2=c.armor+ARMOR_GAIN[k.turn-1];
  if(k.turn>=5 || hp2<=0){ Vec v=fail(); stop_memo.emplace(sk,v); return v; }
  std::unordered_map<Key, double, KeyHash> next_probs;
  for(auto d:next_draws(k.draw,c.discard,5)){
    Key nk{k.turn+1,hp2,c.enemy,armor2,c.focus,c.potion_used,k.potion,k.relic,c.loop,c.orbs,c.vals,d.hand,d.rest,d.discard};
    nk=canonical_key(nk);
    next_probs[nk]+=d.prob;
  }
  std::vector<std::pair<double,Vec>> items;
  items.reserve(next_probs.size());
  for(auto const& it:next_probs) items.push_back({it.second,solve(it.first)});
  Vec v=merge_vec(items);
  stop_memo.emplace(sk,v);
  return v;
}

Vec best_actions(Key const& k, Ctx const& c, std::unordered_map<CtxKey, Vec, CtxKeyHash>& action_memo, std::unordered_map<CtxShape, std::vector<DomEntry>, CtxShapeHash>& dom_memo){
  action_calls++;
  if(trace_every>0 && action_calls%trace_every==0){
    std::cerr<<"trace action_calls "<<action_calls<<" solve_calls "<<solve_calls<<" memo "<<memo.size()<<" turn "<<k.turn<<" enemy "<<c.enemy<<" hand "<<total(c.hand)<<"\n";
  }
  CtxKey ck=action_key_for(k,c);
  auto it=action_memo.find(ck);
  if(it!=action_memo.end()) return it->second;
  CtxShape shape=action_shape_for(k,c);
  int norm_enemy=std::max(0,c.enemy);
  int norm_block=std::min(c.block,DMG[k.turn-1]);
  auto dom_it=dom_memo.find(shape);
  if(dom_it!=dom_memo.end()){
    for(auto const& e:dom_it->second){
      if(e.enemy<=norm_enemy && e.armor<=c.armor && e.block>=norm_block) return e.vec;
    }
  }
  Vec best=resolve_stopped(k,c);
  bool has=true;
  if(best.p[k.turn-1]>0.999999){
    dom_memo[shape].push_back({norm_enemy,c.armor,norm_block,best});
    action_memo.emplace(ck,best);
    return best;
  }
  if(c.enemy>0){
    if(!c.potion_used){
      Ctx out;
      if(use_potion(c,k.potion,out)){
        Vec cand=best_actions(k,out,action_memo,dom_memo);
        if(better(cand,best,has)) best=cand;
      }
    }
    if(c.plays<2 && best.p[k.turn-1]<=0.999999){
      for(int card=0;card<14;card++){
        Ctx out;
        if(play_card(c,card,out)){
          Vec cand=best_actions(k,out,action_memo,dom_memo);
          if(better(cand,best,has)) best=cand;
          if(best.p[k.turn-1]>0.999999) break;
        }
      }
    }
  }
  auto& bucket=dom_memo[shape];
  bucket.erase(std::remove_if(bucket.begin(),bucket.end(),[&](DomEntry const& e){
    return norm_enemy<=e.enemy && c.armor<=e.armor && norm_block>=e.block;
  }),bucket.end());
  bucket.push_back({norm_enemy,c.armor,norm_block,best});
  action_memo.emplace(ck,best);
  return best;
}

Vec solve(Key k){
  solve_calls++;
  if(trace_every>0 && solve_calls%trace_every==0){
    std::cerr<<"trace solve_calls "<<solve_calls<<" action_calls "<<action_calls<<" memo "<<memo.size()<<" turn "<<k.turn<<" enemy "<<k.enemy<<" draw "<<total(k.draw)<<" discard "<<total(k.discard)<<"\n";
  }
  if(k.hp<=0) return fail();
  if(k.enemy<=0) return kill(k.turn);
  k=canonical_key(k);
  auto it=memo.find(k); if(it!=memo.end()) return it->second;
  if(k.turn>=5){
    Ctx terminal;
    terminal.hand=k.hand; terminal.orbs=k.orbs; terminal.vals=k.vals; terminal.enemy=k.enemy; terminal.armor=k.armor;
    terminal.focus=k.focus; terminal.potion_used=k.potion_used; terminal.relic=k.relic; terminal.loop=k.loop;
    Vec v=terminal_can_kill(terminal,k.potion) ? kill(k.turn) : fail();
    memo.emplace(k,v);
    return v;
  }
  Ctx start; start.hand=k.hand; start.discard=k.discard; start.orbs=k.orbs; start.vals=k.vals; start.enemy=k.enemy; start.armor=k.armor; start.focus=k.focus; start.potion_used=k.potion_used; start.relic=k.relic; start.loop=k.loop;
  std::unordered_map<CtxKey, Vec, CtxKeyHash> action_memo;
  std::unordered_map<CtxShape, std::vector<DomEntry>, CtxShapeHash> dom_memo;
  Vec best=best_actions(k,start,action_memo,dom_memo);
  memo.emplace(k,best); return best;
}

Vec result_for(Counts deck,int potion,int relic){
  std::vector<std::pair<double,Vec>> items;
  for(auto d:draw_outcomes(deck,5)){
    Key k{1,PLAYER_HP,ENEMY_HP,0,relic==DATA_DISK?1:0,0,potion,relic,0,{0,0,0},{0,0,0},d.hand,d.rest,{}};
    items.push_back({d.prob,solve(k)});
  }
  return merge_vec(items);
}

void gen_decks_rec(int i,int left,Counts& cur,std::vector<Counts>& out){
  if(i==14){
    int defense=cur[CHILL]+cur[COLD_SNAP]+cur[DEFEND]+cur[LEAP]+cur[COOLHEADED];
    int orb=cur[ZAP]+cur[DARKNESS]+cur[CHILL]+cur[COLD_SNAP]+cur[BALL]+cur[COOLHEADED];
    int release=cur[DUALCAST]+cur[RECURSION];
    int direct=cur[COLD_SNAP]+cur[BALL]+cur[MELTER]+cur[STREAMLINE]+cur[STRIKE];
    if(left==12 && cur[ZAP]>=1 && cur[DUALCAST]==1 && cur[DARKNESS]>=1 && cur[LOOP]==1 && cur[CHILL]==1 && defense>=4 && orb>=6 && release>=2 && direct>=3) out.push_back(cur);
    return;
  }
  for(int k=0;k<=POOL[i] && left+k<=12;k++){ cur[i]=k; gen_decks_rec(i+1,left+k,cur,out); }
  cur[i]=0;
}
std::vector<Counts> decks(){ std::vector<Counts> out; Counts c{}; gen_decks_rec(0,0,c,out); return out; }
int first_turn(Vec v){ for(int i=0;i<5;i++) if(v.p[i]>1e-9) return i+1; return 0; }
std::string deck_str(Counts d){ std::ostringstream os; bool first=true; for(int i=0;i<14;i++) if(d[i]){ if(!first) os<<"、"; first=false; os<<CN[i]; if(d[i]>1) os<<" x"<<(int)d[i]; } return os.str(); }
std::string family(Counts d,int p,int r){
  if(r==DARK_CORE && d[DARKNESS]) return "暗核黑球爆发线";
  if(p==RELEASE_POTION || d[DUALCAST]) return "释放爆发线";
  if(r==GOLD_CABLES && d[LOOP]) return "线缆循环线";
  if(r==DATA_DISK || p==FOCUS_POTION) return "集中闪电线";
  if(d[LEAP]+d[COOLHEADED]+d[CHILL]>=3) return "冰霜防守线";
  return "混合线";
}

int main(int argc,char** argv){
  auto env_int=[](const char* name,int fallback){ const char* v=std::getenv(name); return v ? std::atoi(v) : fallback; };
  trace_every=env_int("GONGDOU_AUDIT_TRACE_EVERY",0);
  if(argc>=19 && std::string(argv[1])=="build"){
    ENEMY_HP=std::atoi(argv[2]); PLAYER_HP=std::atoi(argv[3]);
    int potion=std::atoi(argv[4]), relic=std::atoi(argv[5]);
    Counts d{}; for(int i=0;i<14;i++) d[i]=(unsigned char)std::atoi(argv[6+i]);
    Vec v=result_for(d,potion,relic);
    std::cout<<"enemy_hp "<<ENEMY_HP<<" player_hp "<<PLAYER_HP<<" success "<<success(v)*100<<" first "<<first_turn(v)<<" ["
      <<v.p[0]*100<<","<<v.p[1]*100<<","<<v.p[2]*100<<","<<v.p[3]*100<<","<<v.p[4]*100<<","<<v.p[5]*100<<"] "
      <<family(d,potion,relic)<<" "<<deck_str(d)<<"；"<<POTION_CN[potion]<<"；"<<RELIC_CN[relic]<<"\n";
    return 0;
  }
  if(argc>=3){ ENEMY_HP=std::atoi(argv[1]); PLAYER_HP=std::atoi(argv[2]); }
  int max_builds=argc>=4?std::atoi(argv[3]):env_int("GONGDOU_AUDIT_MAX_BUILDS",0);
  int skip_builds=argc>=5?std::atoi(argv[4]):env_int("GONGDOU_AUDIT_SKIP_BUILDS",0);
  int progress_every=env_int("GONGDOU_AUDIT_PROGRESS_EVERY",50);
  trace_every=env_int("GONGDOU_AUDIT_TRACE_EVERY",0);
  bool no_write=std::getenv("GONGDOU_AUDIT_NO_WRITE") && std::string(std::getenv("GONGDOU_AUDIT_NO_WRITE"))=="1";
  bool clear_memo_per_build=std::getenv("GONGDOU_AUDIT_CLEAR_MEMO_PER_BUILD") && std::string(std::getenv("GONGDOU_AUDIT_CLEAR_MEMO_PER_BUILD"))=="1";
  auto ds=decks(); std::vector<Row> rows; int done=0,seen_builds=0,total_builds=(int)ds.size()*9;
  for(auto d:ds) for(int p=0;p<3;p++) for(int r=0;r<3;r++){
    if(seen_builds++<skip_builds) continue;
    if(clear_memo_per_build) memo.clear();
    Vec v=result_for(d,p,r);
    rows.push_back({d,p,r,first_turn(v),v,success(v),family(d,p,r),deck_str(d)+"；"+POTION_CN[p]+"；"+RELIC_CN[r]});
    done++;
    if(progress_every>0 && (done%progress_every==0||seen_builds==total_builds)){
      auto best_it=std::max_element(rows.begin(),rows.end(),[](auto const& a,auto const& b){ return a.succ<b.succ; });
      std::cout<<"audited "<<done<<"/"<<total_builds;
      if(best_it!=rows.end()) std::cout<<" best_so_far "<<best_it->succ*100<<" first "<<best_it->ft<<" "<<best_it->fam;
      std::cout<<"\n";
      std::cout.flush();
    }
    if(max_builds && done>=max_builds) goto done_loop;
  }
done_loop:
  std::sort(rows.begin(),rows.end(),[](auto&a,auto&b){ if(std::abs(a.succ-b.succ)>1e-12) return a.succ>b.succ; int af=a.ft?a.ft:99,bf=b.ft?b.ft:99; if(af!=bf) return af<bf; return a.vec.p[2]>b.vec.p[2]; });
  int perfect=0; for(auto&r:rows) if(r.succ>0.999999) perfect++;
  Row best_turn[6]; bool has_turn[6]{};
  std::vector<std::string> fam_names={"暗核黑球慢线","释放爆发线","线缆循环线","集中闪电线","冰霜防守线","混合线"};
  std::vector<Row> best_fam(fam_names.size()); std::vector<bool> has_fam(fam_names.size(),false);
  for(auto const& r:rows){
    int t=r.ft; if(t>=0&&t<=5&&(!has_turn[t]||r.succ>best_turn[t].succ)){ best_turn[t]=r; has_turn[t]=true; }
    for(int i=0;i<(int)fam_names.size();i++) if(r.fam==fam_names[i]&&(!has_fam[i]||r.succ>best_fam[i].succ)){ best_fam[i]=r; has_fam[i]=true; }
  }
  std::ostringstream fn;
  fn<<"difficulty8_orb_insulation_audit";
  if(skip_builds||max_builds) fn<<"_part_"<<skip_builds<<"_"<<rows.size();
  fn<<".json";
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
