/* Shared calculations: points are per audit, never per day or per norm. */
(function(root){
  function periods(plan){return Array.isArray(plan?.periods)?plan.periods:plan?.date?[{date:plan.date,start:'',end:''}]:[];}
  function validatePeriods(rows){
    if(!Array.isArray(rows)||!rows.length)throw Error('Informe pelo menos um dia e horário para a auditoria.');
    const sorted=rows.map(p=>{
      if(!/^\d{4}-\d{2}-\d{2}$/.test(p.date||'')||new Date(p.date+'T12:00:00Z').toISOString().slice(0,10)!==p.date)throw Error('Informe datas válidas para todos os períodos.');
      if(!/^([01]\d|2[0-3]):[0-5]\d$/.test(p.start||'')||!/^([01]\d|2[0-3]):[0-5]\d$/.test(p.end||'')||p.end<=p.start)throw Error('O horário final deve ser posterior ao inicial em cada dia.');
      return {date:p.date,start:p.start,end:p.end};
    }).sort((a,b)=>(a.date+a.start).localeCompare(b.date+b.start));
    sorted.forEach((p,i)=>{const prev=sorted[i-1];if(prev&&prev.date===p.date&&prev.end>p.start)throw Error('Há períodos sobrepostos no mesmo dia.');});
    return sorted;
  }
  function assignments(audits,auditors,year){
    const result=[];
    for(const audit of audits.filter(a=>Number(a.year)===Number(year))){
      const source=audit.plan||audit;
      const leader=source.lead_id!=null?auditors.find(a=>a.id===Number(source.lead_id)):auditors.find(a=>a.name===source.lead);
      const ids=new Set();
      const append=(auditor,role,points)=>{if(!auditor||ids.has(auditor.id))return;ids.add(auditor.id);result.push({auditor_id:auditor.id,name:auditor.name,audit_id:audit.id,code:audit.code,area:audit.area,year:audit.year,month:audit.month,role,points,periods:periods(audit.plan),source:audit.plan?(audit.plan.confirmed?'Plano confirmado':'Plano em rascunho'):'Programação'});};
      append(leader,'Auditor líder',2);
      const members=Array.isArray(source.team_ids)?source.team_ids.map(id=>auditors.find(a=>a.id===Number(id))):(source.team||'').split(',').map(name=>auditors.find(a=>a.name===name.trim()));
      members.forEach(a=>append(a,'Auditor',1));
    }
    return result;
  }
  function ranking(audits,auditors,year,ascending=false){
    const list=assignments(audits,auditors,year);
    return auditors.map(a=>{const records=list.filter(x=>x.auditor_id===a.id);return {id:a.id,name:a.name,leaders:records.filter(x=>x.role==='Auditor líder').length,members:records.filter(x=>x.role==='Auditor').length,total:records.reduce((sum,x)=>sum+x.points,0),audits:records.length};}).sort((a,b)=>(ascending?a.total-b.total:b.total-a.total)||a.name.localeCompare(b.name,'pt-BR'));
  }
  function calendarColor(audit){
    if(audit.status==='Concluída')return 'completed';
    const norms=audit.plan?.norms||audit.norms||[];
    const has=value=>norms.some(n=>n.toUpperCase().includes(value));
    if(has('ASME'))return 'asme';
    if(has('ANSN')&&has('SECRETARIA NAVAL'))return 'nuclear';
    if(has('14001')&&has('45001'))return 'sms';
    if(has('9001'))return 'quality';
    return 'default';
  }
  function filterCalendar(audits,{year,norms=[],search=''}={}){const query=search.trim().toLocaleLowerCase();return audits.filter(a=>(year==null||+a.year===+year)&&(!norms.length||norms.some(n=>(a.plan?.norms||a.norms||[]).includes(n)))&&[a.code,a.area,a.supplier_name,a.lead].join(' ').toLocaleLowerCase().includes(query));}
  const api={filterCalendar,calendarColor,periods,validatePeriods,assignments,ranking};
  root.NuclaudPlanning=api;
  if(typeof module!=='undefined')module.exports=api;
})(globalThis);
