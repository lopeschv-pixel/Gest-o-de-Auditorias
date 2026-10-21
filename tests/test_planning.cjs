const assert=require('node:assert/strict');
const {assignments,ranking,validatePeriods}=require('../static/planning.js');
const auditors=[{id:1,name:'Ana'},{id:2,name:'Bruno'},{id:3,name:'Carla'},{id:4,name:'Davi'}];
const periods=[{date:'2027-03-11',start:'13:00',end:'17:00'},{date:'2027-03-10',start:'08:00',end:'12:00'}];
const audits=[{id:10,code:'AUDI-I-001-2027',year:2027,month:3,lead_id:1,team_ids:[2],observer_ids:[3],norms:['ISO 9001:2015','ISO 14001'],plan:{lead_id:2,team_ids:[1],periods,confirmed:true}},{id:11,code:'AUDI-F-001-2027',year:2027,month:5,lead_id:1,team_ids:[2]},{id:12,year:2028,lead_id:1,team_ids:[2]}];
const totals=ranking(audits,auditors,2027);
assert.deepEqual(totals.map(a=>[a.name,a.total]),[['Ana',3],['Bruno',3],['Carla',0],['Davi',0]]);
assert.equal(assignments(audits,auditors,2027).length,4);
assert.equal(totals[0].leaders,1);assert.equal(totals[0].members,1);
assert.deepEqual(ranking(audits,auditors,2027,true).map(a=>a.total),[0,0,3,3]);
assert.equal(ranking(audits,auditors,2028)[0].total,2);
assert.equal(validatePeriods(periods)[0].date,'2027-03-10');
assert.throws(()=>validatePeriods([{date:'2027-03-10',start:'17:00',end:'08:00'}]));
assert.throws(()=>validatePeriods([{date:'2027-03-10',start:'08:00',end:'12:00'},{date:'2027-03-10',start:'11:00',end:'13:00'}]));
assert.equal(validatePeriods([{date:'2027-03-10',start:'08:00',end:'12:00'},{date:'2027-03-10',start:'12:00',end:'17:00'}]).length,2);
assert.throws(()=>validatePeriods([]));
assert.throws(()=>validatePeriods([{date:'2027-02-30',start:'08:00',end:'12:00'}]));
const old=[{id:99,year:2027,month:7,lead:'Ana',team:'Bruno'}];
assert.deepEqual(ranking(old,auditors,2027).map(a=>a.total),[2,1,0,0]);
assert.equal(ranking([{...audits[1],team_ids:[1,2,2]}],auditors,2027)[0].total,2);
console.log('PASS: points per audit, plan replacement, year filters, zero-score auditors, sorting, legacy records and multiple-day validation.');

const {calendarColor}=require('../static/planning.js');
for(const [norms,color] of [[['ISO 9001:2015'],'quality'],[['ANSN 1.16','Secretaria Naval'],'nuclear'],[['ISO 14001','ISO 45001'],'sms'],[['ASME III'],'asme'],[['Outra norma'],'default']]){
 assert.equal(calendarColor({norms,status:'Programada'}),color);
 assert.equal(calendarColor({norms,status:'Concluída'}),'completed');
}
assert.equal(calendarColor({norms:['ISO 9001:2015'],plan:{norms:['ASME III']}}),'asme');
