(function(root){
  function canManage(user,audit){return !!user&&(user.role==='admin'||user.auditor_id!=null&&Number((audit.plan||audit).lead_id)===Number(user.auditor_id));}
  function checkUpdate(user,previous,updated){
    if(user.role==='admin')return;
    if(previous.status==='Concluída')throw Error('Somente administradores podem alterar um relatório concluído.');
    if(!canManage(user,previous))throw Error('Você só pode gerar planos e relatórios das auditorias em que é líder.');
    const oldPlan=previous.plan||previous,newPlan=updated.plan;
    if(newPlan){const dates=new Set(['date','periods','confirmed']);const keys=previous.plan?new Set([...Object.keys(oldPlan),...Object.keys(newPlan)]):new Set(Object.keys(newPlan));for(const key of keys)if(!dates.has(key)&&JSON.stringify(oldPlan[key])!==JSON.stringify(newPlan[key]))throw Error('O auditor líder só pode alterar os dias e horários do plano.');}
    const allowed=new Set(['plan','report','status','id']);
    for(const key of new Set([...Object.keys(previous),...Object.keys(updated)]))if(!allowed.has(key)&&JSON.stringify(previous[key])!==JSON.stringify(updated[key]))throw Error('Somente administradores podem alterar a programação.');
    if(!updated.plan||Number(updated.plan.lead_id)!==Number(user.auditor_id))throw Error('O usuário não pode transferir a liderança da auditoria.');
  }
  const api={canManage,checkUpdate};root.NuclaudAccess=api;if(typeof module!=='undefined')module.exports=api;
})(globalThis);
