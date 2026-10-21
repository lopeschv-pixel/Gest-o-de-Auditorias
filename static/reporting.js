(function(root){
  const qualifications={not_qualified:'Não qualificado',one_year:'Qualificado por um ano',three_years:'Qualificado por três anos'};
  const kinds=['Conformidade','Não conformidade','Observação','Oportunidade de melhoria'];
  const labels={Conformidade:{description:'Descrição da conformidade',evidence:'Evidência encontrada'},'Não conformidade':{description:'Descrição da não conformidade',evidence:'Evidências objetivas'},Observação:{description:'Observação'},'Oportunidade de melhoria':{description:'Oportunidade de melhoria'}};
  const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const text=v=>typeof v==='string'?v.trim():'';
  function validateFinding(body,audit,requirements){
    if(!kinds.includes(body.kind))throw Error('Selecione uma classificação válida.');
    const description=text(body.description);
    if(body.kind!=='Conformidade'&&!description)throw Error('Preencha o campo '+labels[body.kind].description+'.');
    const result={audit_id:audit.id,kind:body.kind,area:audit.area,description:body.kind==='Conformidade'?'':description,auditor_name:(audit.plan||audit).lead||audit.lead||''};
    if(['Conformidade','Não conformidade'].includes(body.kind)){
      const ids=body.requirement_ids;
      const allowed=audit.plan?.requirement_ids;
      if(!Array.isArray(ids)||!ids.length||new Set(ids).size!==ids.length)throw Error('Selecione um ou mais requisitos do plano.');
      const selected=ids.map(id=>requirements.find(r=>r.id===+id));
      if(selected.some(r=>!r||(allowed?.length?!allowed.includes(r.id):!audit.plan?.norms.includes(r.norm))))throw Error('Selecione requisitos que pertencem ao plano da auditoria.');
      if(!text(body.evidence))throw Error('Preencha o campo '+labels[body.kind].evidence+'.');
      result.requirement_ids=ids.map(Number);result.requirements=selected.map(r=>`${r.norm} · ${r.code}${r.title?' — '+r.title:''}`).join('\n');result.evidence=text(body.evidence);
      if(body.kind==='Não conformidade'){if(!text(body.pac))throw Error('Informe o número do PAC da não conformidade.');result.pac=text(body.pac);}
    }
    return result;
  }
  function findingContent(f){
    const meta=labels[f.kind]||{description:'Descrição'};
    return `${['Conformidade','Não conformidade'].includes(f.kind)?`<div class="finding-field"><h4>Requisitos relacionados</h4><p>${esc(f.requirements||'Não informado')}</p></div>`:''}${f.kind!=='Conformidade'?`<div class="finding-field"><h4>${meta.description}</h4><p>${esc(f.description)}</p></div>`:''}${meta.evidence?`<div class="finding-field"><h4>${meta.evidence}</h4><p>${esc(f.evidence||'Não informado')}</p></div>`:''}${f.kind==='Não conformidade'?`<div class="finding-field"><h4>Número do PAC</h4><p>${esc(f.pac||'Não informado')}</p></div>`:''}`;
  }
  function findingCards(findings,editable=false){return findings.map((f,i)=>`<article class="finding-card"><div class="finding-card-heading"><div class="finding-heading-label"><strong>Constatação ${String(i+1).padStart(2,'0')}</strong><span> · ${esc(f.kind)}</span></div>${editable?`<div class="finding-actions"><button type="button" class="text-button" data-edit-finding="${esc(f.id)}">Editar constatação</button><button type="button" class="text-button danger" data-delete-finding="${esc(f.id)}">Excluir constatação</button></div>`:''}</div>${findingContent(f)}</article>`).join('');}
  function periodRows(plan){const periods=Array.isArray(plan?.periods)?plan.periods:plan?.date?[{date:plan.date}]:[];return periods.map(p=>`<tr><td>${esc((p.date||'').split('-').reverse().join('/'))}</td><td>${esc(p.start||'Não informado')}</td><td>${esc(p.end||'Não informado')}</td></tr>`).join('');}
  function info(rows){return `<dl class="doc-info">${rows.map(([label,value])=>`<div><dt>${esc(label)}</dt><dd>${esc(value||'Não informado')}</dd></div>`).join('')}</dl>`;}
  function section(title,content){return `<section class="doc-section"><h2>${title}</h2>${content}</section>`;}
  function documentTitle(audit,type){return type==='report'?(audit.audit_type==='F'?'Relatório de Auditoria de Fornecedores':'Relatório de Auditoria Interna'):'Plano de auditoria';}
  function renderDocument(audit,findings,type,issuedOn){
    const p=audit.plan||audit,report=type==='report';
    const title=documentTitle(audit,type);
    const state=report?(audit.status==='Concluída'?'Concluído':'Em elaboração'):'Emitido';
    const relevant=findings.filter(f=>+f.audit_id===+audit.id);
    const lines=String(p.requirements||'').split('\n').filter(Boolean);
    const header=`<div class="doc-header"><div><div class="doc-brand">${globalThis.NUCLAUD_LOGO?`<img class="doc-logo" src="${esc(globalThis.NUCLAUD_LOGO)}" alt="Logotipo da NUCLEP">`:""}</div><h1>${title}</h1><p>${esc(audit.audit_type==='F'?'Auditoria de fornecedores':'Auditoria interna')}</p></div><div class="doc-reference"><strong>${esc(audit.code)}</strong><span>${state}</span><small>Emissão: ${esc(issuedOn)}</small></div></div>`;
    const identification=section('1. Identificação da auditoria',info([...(audit.audit_type==='F'?[['Nome do fornecedor',audit.supplier_name],['CNPJ do fornecedor',audit.supplier_cnpj],['Endereço do fornecedor',audit.supplier_address],['Escopo de fornecimento',audit.supply_scope]]:[['Áreas',audit.area],['Macroprocessos',p.macroprocesses??audit.macroprocesses],['Processos',p.processes??audit.processes]]),['Normas e referenciais',(p.norms||[]).join(' · ')],['Local',p.location],['Nome do gestor',p.manager_name],['E-mail do gestor',p.manager_email||p.email]]));
    const team=section('2. Equipe auditora e participantes',info([['Auditor líder',p.lead||audit.lead],['Auditores',(p.team??audit.team)||'Não há outros auditores'],...(p.observers?[['Observadores',p.observers]]:[]),...(report&&audit.report?.participants?[['Participantes',audit.report.participants]]:[])]));
    const dates=section('3. Dias e horários',`<table class="doc-table"><thead><tr><th>Data</th><th>Início</th><th>Fim</th></tr></thead><tbody>${periodRows(p)}</tbody></table>`);
    const requirements=section('4. Requisitos previstos no plano',lines.length?`<ul class="doc-requirements">${lines.map(line=>`<li>${esc(line)}</li>`).join('')}</ul>`:'<p>Não informados.</p>');
    let body;
    if(report){const summary=`<div class="doc-counts">${kinds.map(kind=>`<span>${esc(kind)}: <strong>${relevant.filter(f=>f.kind===kind).length}</strong></span>`).join('')}</div>`;body=section('5. Constatações',summary+(relevant.length?findingCards(relevant):'<p>Nenhuma constatação registrada.</p>'))+(audit.audit_type==='F'?section('6. Conclusão',`${info([['Resultado da qualificação',qualifications[audit.report?.qualification_result]||'Ainda não definido']])}<p class="doc-text"><strong>Observações da conclusão:</strong><br> ${esc(audit.report?.conclusion||'Sem observações.')}</p>${info([['Número do RQF',audit.report?.rqf]])}`):'');}
    else body=audit.audit_type==='F'?section('5. Objetivo e escopo',`<p class="doc-text">${esc(p.scope||'Não informado')}</p>`):'';
    return `<article class="audit-document">${header}${identification}${team}${dates}${requirements}${body}<section class="doc-signature"><div></div><span>Assinatura do auditor líder</span></section><div class="doc-footer"><span></span><strong>${esc(audit.code)}</strong></div></article>`;
  }
  function missingRequirements(audit,findings){const covered=new Set(findings.filter(f=>+f.audit_id===+audit.id&&['Conformidade','Não conformidade'].includes(f.kind)).flatMap(f=>f.requirement_ids||[]).map(Number));return (audit.plan?.requirement_ids||[]).filter(id=>!covered.has(+id));}
  function assertComplete(audit,findings,requirements=[]){const ids=audit.plan?.requirement_ids||[];if(!ids.length)throw Error('O plano deve definir os requisitos auditados.');const missing=missingRequirements(audit,findings);if(missing.length){const names=missing.map(id=>{const r=requirements.find(r=>+r.id===+id);return r?`${r.norm} · ${r.code}${r.title?' — '+r.title:''}`:`Requisito ${id}`;});throw Error(`Classifique todos os requisitos em conformidade ou não conformidade. Faltam ${missing.length} requisito(s):\n`+names.join('\n'));}if(audit.audit_type==='F'&&!Object.keys(qualifications).includes(audit.report?.qualification_result))throw Error('Selecione o resultado da qualificação do fornecedor em Editar relatório e clique em Salvar relatório.');}
  const api={documentTitle,qualifications,missingRequirements,assertComplete,kinds,labels,validateFinding,findingCards,renderDocument};root.NuclaudReporting=api;if(typeof module!=='undefined')module.exports=api;
})(globalThis);
