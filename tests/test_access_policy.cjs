const assert=require('node:assert/strict');
const {canManage,checkUpdate}=require('../static/access-policy.js');
const admin={role:'admin'},user={role:'user',auditor_id:10};
const audit={id:1,year:2027,month:3,area:'Qualidade',code:'AUDI-I-001-2027',lead_id:10,team_ids:[11],norms:['ISO 9001:2015'],status:'Programada'};
assert.equal(canManage(null,audit),false);
assert.equal(canManage(admin,{lead_id:99}),true);
assert.equal(canManage(user,audit),true);
assert.equal(canManage(user,{...audit,lead_id:11,team_ids:[10]}),false);
assert.equal(canManage(user,{...audit,plan:{lead_id:11}}),false);
const plan={...audit,plan:{lead_id:10,date:'2027-03-10'},status:'Plano em rascunho'};
assert.doesNotThrow(()=>checkUpdate(user,audit,plan));
assert.throws(()=>checkUpdate(user,audit,{...plan,month:4}));
assert.throws(()=>checkUpdate(user,audit,{...plan,role:'admin'}));
assert.throws(()=>checkUpdate(user,audit,{...plan,plan:{lead_id:11}}));
assert.throws(()=>checkUpdate(user,{...audit,lead_id:11},plan));
assert.doesNotThrow(()=>checkUpdate(admin,audit,{...audit,month:4,lead_id:11}));
console.log('PASS: demo policies distinguish administration, audit leadership, membership and immutable program fields.');

assert.throws(()=>checkUpdate(user,{...plan,status:'Concluída'},{...plan,status:'Em execução'}));
assert.doesNotThrow(()=>checkUpdate(admin,{...plan,status:'Concluída'},{...plan,status:'Em execução'}));

const basePlan={...plan,plan:{lead_id:10,date:'2027-03-10',norms:['ISO 9001:2015'],requirements:'8.4',location:'Sala A'}};
assert.doesNotThrow(()=>checkUpdate(user,basePlan,{...basePlan,plan:{...basePlan.plan,date:'2027-03-11',confirmed:false}}));
for(const [key,value] of [['location','Sala B'],['norms',['ISO 45001']],['requirements','9.1'],['manager_name','Novo gestor'],['team_ids',[15]]]){
 assert.throws(()=>checkUpdate(user,basePlan,{...basePlan,plan:{...basePlan.plan,[key]:value}}));
 assert.doesNotThrow(()=>checkUpdate(admin,basePlan,{...basePlan,plan:{...basePlan.plan,[key]:value}}));
}
