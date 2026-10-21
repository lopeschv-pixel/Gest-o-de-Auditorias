/* jsPDF is bundled locally; email drafts contain the PDF, never an external print URL. */
(function(root){
 function pdf(a,findings){
  const doc=new root.jspdf.jsPDF({unit:'mm',format:'a4'});let y=18;
  const container=document.createElement('div');container.innerHTML=NuclaudReporting.renderDocument(a,findings,'plan',new Date().toLocaleDateString('pt-BR'));
  function line(value,bold=false){doc.setFont('helvetica',bold?'bold':'normal');doc.setFontSize(bold?12:10);for(const part of doc.splitTextToSize(value,178)){if(y>275){doc.addPage();y=18;}doc.text(part,16,y);y+=5;}y+=2;}
  line('NUCLAUD — Plano de auditoria',true);line(a.code,true);
  container.querySelectorAll('.doc-section').forEach(section=>{line(section.querySelector('h2').textContent,true);section.querySelectorAll('.doc-info>div').forEach(item=>line(item.querySelector('dt').textContent+': '+item.querySelector('dd').textContent));section.querySelectorAll('tbody tr').forEach(row=>line([...row.cells].map(c=>c.textContent).join(' | ')));section.querySelectorAll('li,.doc-text').forEach(item=>line(item.textContent));});
  if(y>245){doc.addPage();y=18;}y+=12;line('________________________________________');line(a.plan.lead);line('Assinatura do auditor líder · Data: ____/____/________');
  return doc.output('datauristring').split(',')[1];
 }
 const b64=text=>btoa(String.fromCharCode(...new TextEncoder().encode(text)));
 function message(a,findings){const email=(a.plan.manager_email||a.plan.email||'').trim();if(!/^[^\s<>@]+@[^\s<>@]+\.[^\s<>@]+$/.test(email))throw Error('Informe um e-mail válido do gestor no plano.');const boundary='NUCLAUD_'+Date.now();const encoded=pdf(a,findings).match(/.{1,76}/g).join('\r\n');return ['X-Unsent: 1','To: '+email,'Subject: =?UTF-8?B?'+b64('Plano de auditoria — '+a.code)+'?=','MIME-Version: 1.0','Content-Type: multipart/mixed; boundary="'+boundary+'"','','--'+boundary,'Content-Type: text/plain; charset=UTF-8','Content-Transfer-Encoding: base64','',b64(`Prezado(a) ${a.plan.manager_name||'gestor(a)'},\n\nSegue em anexo o plano de auditoria ${a.code} para conhecimento.\n\nAtenciosamente,\n${a.plan.lead}`),'--'+boundary,'Content-Type: application/pdf; name="Plano-'+a.code+'.pdf"','Content-Disposition: attachment; filename="Plano-'+a.code+'.pdf"','Content-Transfer-Encoding: base64','',encoded,'--'+boundary+'--',''].join('\r\n');}
 function prepare(a,findings){const blob=new Blob([message(a,findings)],{type:'message/rfc822'});const url=URL.createObjectURL(blob),link=document.createElement('a');link.href=url;link.download='Plano-'+a.code+'.eml';link.click();setTimeout(()=>URL.revokeObjectURL(url),30000);}
 root.NuclaudEmail={pdf,message,prepare};
})(globalThis);
