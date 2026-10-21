const assert=require('node:assert/strict');
const R=require('../static/reporting.js');
const requirements=[{id:1,norm:'ISO 9001:2015',code:'8.4',title:'Controle de fornecedores'},{id:2,norm:'ISO 9001:2015',code:'7.2',title:'Competência'}];
const audit={id:7,code:'AUDI-I-001-2027',audit_type:'I',area:'Qualidade',lead:'Líder previsto',team:'Equipe prevista',macroprocesses:'Gestão',processes:'Gestão › Qualidade',plan:{lead:'Ana',team:'Bruno',norms:['ISO 9001:2015'],requirement_ids:[1],requirements:'ISO 9001:2015 · 8.4 — Controle de fornecedores',periods:[{date:'2027-03-10',start:'08:00',end:'12:00'}]},report:{participants:'Pessoa auditada',conclusion:'CONCLUSAO ANTIGA NAO MOSTRAR'}};
const base={kind:'Conformidade',description:'Critério atendido',requirement_ids:[1],evidence:'Registro verificado'};
const conform=R.validateFinding(base,audit,requirements);
assert.equal(conform.auditor_name,'Ana');assert.ok(conform.requirements.includes('Controle de fornecedores'));
assert.throws(()=>R.validateFinding({...base,requirement_ids:[2]},audit,requirements));
assert.throws(()=>R.validateFinding({...base,evidence:''},audit,requirements));
assert.throws(()=>R.validateFinding({...base,kind:'Constatação'},audit,requirements));
assert.throws(()=>R.validateFinding({...base,kind:'Não conformidade',pac:''},audit,requirements));
const nc=R.validateFinding({...base,kind:'Não conformidade',pac:'PAC-123'},audit,requirements);
assert.equal(nc.pac,'PAC-123');
for(const kind of ['Observação','Oportunidade de melhoria']){
 const result=R.validateFinding({...base,kind,pac:'DADO OCULTO'},audit,requirements);
 assert.deepEqual(Object.keys(result).sort(),['area','audit_id','auditor_name','description','kind'].sort());
 const card=R.findingCards([result]);assert.ok(!card.includes('Requisitos relacionados'));assert.ok(!card.includes('Evidências objetivas'));assert.ok(!card.includes('Número do PAC'));
}
const internal=R.renderDocument(audit,[conform,nc],'report','21/10/2026');
assert.ok(internal.includes('Relatório de auditoria'));
assert.ok(!internal.includes('Plano de auditoria'));
assert.ok(!internal.includes('CONCLUSAO ANTIGA'));assert.ok(!internal.includes('Conclusão da auditoria'));
assert.ok(internal.includes('Ana'));assert.ok(internal.includes('Bruno'));assert.ok(!internal.includes('Equipe prevista'));
assert.ok(internal.includes('Gestão › Qualidade'));assert.ok(internal.includes('Evidências objetivas'));
const supplier=R.renderDocument({...audit,audit_type:'F',report:{conclusion:'Fornecedor avaliado'}},[],'report','21/10/2026');
assert.ok(supplier.includes('Conclusão da auditoria de fornecedores'));assert.ok(supplier.includes('Fornecedor avaliado'));
const plan=R.renderDocument(audit,[],'plan','21/10/2026');assert.ok(plan.includes('Plano de auditoria'));assert.ok(!plan.includes('Constatação 01'));
const safe=R.findingCards([{...nc,description:'<script>unsafe</script>'}]);assert.ok(safe.includes('&lt;script&gt;'));
const removed=R.renderDocument({...audit,plan:{...audit.plan,team:''}},[],'report','21/10/2026');assert.ok(!removed.includes('Equipe prevista'));
console.log('PASS: classification-specific requirements and fields, hidden-field cleanup, plan team, supplier-only conclusion and document structure.');

assert.equal(conform.description,'');assert.ok(!R.findingCards([conform]).includes('Descrição da conformidade'));
assert.throws(()=>R.assertComplete(audit,[]));
assert.throws(()=>R.assertComplete(audit,[{audit_id:7,kind:'Observação',requirement_ids:[1]}]));
R.assertComplete(audit,[conform]);
assert.throws(()=>R.assertComplete({...audit,plan:{...audit.plan,requirement_ids:[1,2]}},[conform]));
assert.ok(internal.includes('Assinatura do auditor líder'));
assert.ok(!plan.includes('Objetivo e escopo'));
