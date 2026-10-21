/* jsPDF is bundled locally; email drafts contain the PDF, never an external print URL. */
(function(root){
 function pdf(a,findings,type='plan'){
  const doc=new root.jspdf.jsPDF({unit:'mm',format:'a4'}),title=type==='report'?'Relatório de auditoria':'Plano de auditoria';let y=38;
  const issued=new Date().toLocaleDateString('pt-BR',{timeZone:'America/Sao_Paulo'});
  const container=document.createElement('div');container.innerHTML=NuclaudReporting.renderDocument(a,findings,type,issued);
  function header(){doc.setTextColor(33,91,66);doc.setFont('helvetica','bold');doc.setFontSize(15);doc.text('NUCLAUD · NUCLEP',16,17);doc.setFontSize(10);doc.text(a.code,194,17,{align:'right'});doc.setFontSize(14);doc.text(title,16,26);doc.setFont('helvetica','normal');doc.setFontSize(8);doc.text('Emissão: '+issued,194,26,{align:'right'});doc.setDrawColor(33,91,66);doc.line(16,30,194,30);y=38;}
  function room(height){if(y+height>250){doc.addPage();header();}}
  function line(value,bold=false){doc.setFont('helvetica',bold?'bold':'normal');doc.setFontSize(bold?10:9.5);const lines=doc.splitTextToSize(String(value||'Não informado'),178);for(const part of lines){room(5);doc.setFont('helvetica',bold?'bold':'normal');doc.setFontSize(bold?10:9.5);doc.setTextColor(30,51,40);doc.text(part,16,y);y+=4.8;}y+=2;}
  function heading(value){room(16);doc.setFillColor(237,243,239);doc.rect(16,y-4,178,8,'F');doc.setFont('helvetica','bold');doc.setFontSize(11);doc.setTextColor(33,91,66);doc.text(value,19,y+1);y+=10;}
  header();
  container.querySelectorAll('.doc-section').forEach(section=>{
   for(const item of section.children){
    if(item.tagName==='H2')heading(item.textContent);
    else if(item.classList.contains('doc-info'))item.querySelectorAll(':scope>div').forEach(row=>{room(10);line(row.querySelector('dt').textContent+': '+row.querySelector('dd').textContent);});
    else if(item.tagName==='TABLE'){line('Data                         Início                         Fim',true);item.querySelectorAll('tbody tr').forEach(row=>line([...row.cells].map(c=>c.textContent).join('                         ')));}
    else if(item.tagName==='UL')item.querySelectorAll('li').forEach(row=>line('• '+row.textContent));
    else if(item.classList.contains('finding-card')){room(20);line(item.querySelector('.finding-heading-label').textContent,true);item.querySelectorAll('.finding-field').forEach(field=>{room(12);line(field.querySelector('h4').textContent,true);line(field.querySelector('p').textContent);});y+=3;}
    else if(item.classList.contains('doc-counts'))line([...item.children].map(row=>row.textContent).join(' · '));
    else if(item.tagName==='P')line(item.textContent);
   }
   y+=3;
  });
  // All content stops above 250 mm. Reserve a fixed signature area on the final page.
  doc.setDrawColor(70,96,82);doc.line(60,267,150,267);doc.setFont('helvetica','normal');doc.setTextColor(30,51,40);doc.setFontSize(9);doc.text('Assinatura do auditor líder',105,273,{align:'center'});
  const count=doc.getNumberOfPages();for(let page=1;page<=count;page++){doc.setPage(page);doc.setDrawColor(203,217,207);doc.line(16,282,194,282);doc.setTextColor(99,123,106);doc.setFont('helvetica','normal');doc.setFontSize(8);doc.text('NUCLAUD · '+a.code,16,287);doc.text('Página '+page+' de '+count,194,287,{align:'right'});}
  return doc.output('datauristring').split(',')[1];
 }
 function download(a,findings,type){const bytes=Uint8Array.from(atob(pdf(a,findings,type)),c=>c.charCodeAt(0));const url=URL.createObjectURL(new Blob([bytes],{type:'application/pdf'}));const link=document.createElement('a');link.href=url;link.download=(type==='report'?'Relatorio-':'Plano-')+a.code+'.pdf';link.click();setTimeout(()=>URL.revokeObjectURL(url),30000);}
 const b64=text=>btoa(String.fromCharCode(...new TextEncoder().encode(text)));
 function message(a,findings){const email=(a.plan.manager_email||a.plan.email||'').trim();if(!/^[^\s<>@]+@[^\s<>@]+\.[^\s<>@]+$/.test(email))throw Error('Informe um e-mail válido do gestor no plano.');const boundary='NUCLAUD_'+Date.now();const encoded=pdf(a,findings).match(/.{1,76}/g).join('\r\n');return ['X-Unsent: 1','To: '+email,'Subject: =?UTF-8?B?'+b64('Plano de auditoria — '+a.code)+'?=','MIME-Version: 1.0','Content-Type: multipart/mixed; boundary="'+boundary+'"','','--'+boundary,'Content-Type: text/plain; charset=UTF-8','Content-Transfer-Encoding: base64','',b64(`Prezado(a) ${a.plan.manager_name||'gestor(a)'},\n\nSegue em anexo o plano de auditoria ${a.code} para conhecimento.\n\nAtenciosamente,\n${a.plan.lead}`),'--'+boundary,'Content-Type: application/pdf; name="Plano-'+a.code+'.pdf"','Content-Disposition: attachment; filename="Plano-'+a.code+'.pdf"','Content-Transfer-Encoding: base64','',encoded,'--'+boundary+'--',''].join('\r\n');}
 function prepare(a,findings){const blob=new Blob([message(a,findings)],{type:'message/rfc822'});const url=URL.createObjectURL(blob),link=document.createElement('a');link.href=url;link.download='Plano-'+a.code+'.eml';link.click();setTimeout(()=>URL.revokeObjectURL(url),30000);}
 root.NuclaudEmail={pdf,download,message,prepare};
})(globalThis);
