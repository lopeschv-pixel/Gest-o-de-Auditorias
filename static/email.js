/* Local PDF generation with a shared logo and signature on the final page. */
(function(root){
 function pdf(a,findings,type='plan'){
  const doc=new root.jspdf.jsPDF({unit:'mm',format:'a4'}),title=NuclaudReporting.documentTitle(a,type);let y=38;
  const issued=new Date().toLocaleDateString('pt-BR',{timeZone:'America/Sao_Paulo'});
  const container=document.createElement('div');container.innerHTML=NuclaudReporting.renderDocument(a,findings,type,issued);
  function header(){const logo=root.NUCLAUD_LOGO;if(logo){const props=doc.getImageProperties(logo),scale=Math.min(42/props.width,18/props.height);doc.addImage(logo,'PNG',16,9,props.width*scale,props.height*scale);}doc.setTextColor(33,91,66);doc.setFont('helvetica','bold');doc.setFontSize(15);doc.setFontSize(10);doc.text(a.code,194,17,{align:'right'});doc.setFontSize(14);doc.text(title,16,root.NUCLAUD_LOGO?34:26);doc.setFont('helvetica','normal');doc.setFontSize(8);doc.text('Emissão: '+issued,194,root.NUCLAUD_LOGO?34:26,{align:'right'});doc.setDrawColor(33,91,66);const bottom=root.NUCLAUD_LOGO?38:30;doc.line(16,bottom,194,bottom);y=bottom+8;}
  function room(height){if(y+height>250){doc.addPage();header();}}
  function line(value,bold=false){doc.setFont('helvetica',bold?'bold':'normal');doc.setFontSize(bold?10:9.5);const lines=doc.splitTextToSize(String(value||'Não informado'),178);for(const part of lines){room(5);doc.setFont('helvetica',bold?'bold':'normal');doc.setFontSize(bold?10:9.5);doc.setTextColor(30,51,40);doc.text(part,16,y);y+=4.8;}y+=2;}
  function heading(value){room(16);doc.setFillColor(237,243,239);doc.rect(16,y-4,178,8,'F');doc.setFont('helvetica','bold');doc.setFontSize(11);doc.setTextColor(33,91,66);doc.text(value,19,y+1);y+=10;}
  doc.setProperties({title});
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
  const count=doc.getNumberOfPages();for(let page=1;page<=count;page++){doc.setPage(page);doc.setDrawColor(203,217,207);doc.line(16,282,194,282);doc.setTextColor(99,123,106);doc.setFont('helvetica','normal');doc.setFontSize(8);doc.text(a.code,16,287);doc.text('Página '+page+' de '+count,194,287,{align:'right'});}
  return doc.output('datauristring').split(',')[1];
 }
 function download(a,findings,type){const bytes=Uint8Array.from(atob(pdf(a,findings,type)),c=>c.charCodeAt(0));const url=URL.createObjectURL(new Blob([bytes],{type:'application/pdf'}));const link=document.createElement('a');link.href=url;link.download=(type==='report'?'Relatorio-':'Plano-')+a.code+'.pdf';link.click();setTimeout(()=>URL.revokeObjectURL(url),30000);}
 function programPdf(audits,{year,norms=[],search=''}={}){
  const doc=new root.jspdf.jsPDF({orientation:'landscape',unit:'mm',format:'a4'}),title='Programa de Auditorias Internas — '+year;
  const rows=[...audits].sort((a,b)=>+a.month-+b.month||a.code.localeCompare(b.code,'pt-BR',{numeric:true}));
  const months=['Janeiro','Fevereiro','Março','Abril','Maio','Junho','Julho','Agosto','Setembro','Outubro','Novembro','Dezembro'];
  const widths=[20,34,50,37,43,65,20],labels=['Mês previsto','Código','Áreas / fornecedor e processos','Normas','Equipe auditora','Requisitos','Situação'];let y=0;
  const filterText='Normas: '+(norms.length?norms.join(' · '):'Todas as normas')+(search.trim()?' | Busca: '+search.trim():'')+' | Total: '+rows.length;
  function header(){
   const logo=root.NUCLAUD_LOGO;if(logo){const image=doc.getImageProperties(logo),scale=Math.min(40/image.width,16/image.height);doc.addImage(logo,'PNG',14,8,image.width*scale,image.height*scale);}
   doc.setFont('helvetica','bold');doc.setFontSize(12);doc.setTextColor(33,91,66);doc.setFontSize(15);doc.text(title,14,32);
   doc.setFont('helvetica','normal');doc.setFontSize(8.5);const displayedFilter=filterText.length>700?filterText.slice(0,697)+'...':filterText;const lines=doc.splitTextToSize(displayedFilter,269);doc.text(lines,14,39);y=43+lines.length*4;
   doc.setFillColor(33,91,66);doc.rect(14,y,269,12,'F');doc.setFont('helvetica','bold');doc.setTextColor(255);let x=14;
   labels.forEach((label,i)=>{doc.text(doc.splitTextToSize(label,widths[i]-4),x+2,y+4.5);x+=widths[i];});y+=12;
  }
  doc.setProperties({title});
  header();
  if(!rows.length){doc.setFont('helvetica','normal');doc.setTextColor(50);doc.text('Nenhuma auditoria encontrada para os filtros selecionados.',16,y+10);}
  rows.forEach((a,index)=>{
   const p=a.plan||a;
   const identity=a.audit_type==='F'?(a.supplier_name||'Fornecedor não informado'):[a.area,p.macroprocesses??a.macroprocesses,p.processes??a.processes].filter(Boolean).join('\n');
   const team=['Líder: '+(p.lead||a.lead||'—'),(p.team??a.team)?'Auditores: '+(p.team??a.team):'',p.observers?'Observadores: '+p.observers:''].filter(Boolean).join('\n');
   const values=[months[+a.month-1]||'—',a.code,identity,(p.norms||a.norms||[]).join('\n'),team,p.requirements||a.requirements||'—',a.status==='Concluída'?'Realizada':a.status==='Plano em rascunho'?'Planejada':a.status||'Programada'];
   doc.setFont('helvetica','normal');doc.setFontSize(8);const cells=values.map((value,i)=>doc.splitTextToSize(String(value||'—'),widths[i]-4));const maxLines=Math.max(...cells.map(c=>c.length));let offset=0;
   while(offset<maxLines){
    const capacity=Math.floor((191-y-4)/3.7);if(capacity<Math.min(3,maxLines-offset)){doc.addPage();header();continue;}
    const count=Math.min(capacity,maxLines-offset),height=Math.max(11,count*3.7+4);doc.setFillColor(index%2?247:255,index%2?250:255,index%2?248:255);doc.setDrawColor(210,222,215);doc.setLineWidth(.15);doc.setFont('helvetica','normal');doc.setFontSize(8);doc.setTextColor(30,51,40);let x=14;
    cells.forEach((cell,i)=>{doc.setFillColor(index%2?247:255,index%2?250:255,index%2?248:255);doc.rect(x,y,widths[i],height,'FD');const lines=cell.slice(offset,offset+count);if(i===1&&offset&&lines.length===0)lines.push(a.code,'(continuação)');if(lines.length)doc.text(lines,x+2,y+4.4,{lineHeightFactor:1.31});x+=widths[i];});y+=height;offset+=count;
   }
  });
  const total=doc.getNumberOfPages();for(let i=1;i<=total;i++){doc.setPage(i);doc.setDrawColor(203,217,207);doc.line(14,198,283,198);doc.setFont('helvetica','normal');doc.setFontSize(8);doc.setTextColor(99,123,106);doc.text('Programa de auditorias · '+year,14,203);doc.text('Página '+i+' de '+total,283,203,{align:'right'});}
  return doc.output('datauristring').split(',')[1];
 }
 function downloadProgram(audits,options){const bytes=Uint8Array.from(atob(programPdf(audits,options)),c=>c.charCodeAt(0));const url=URL.createObjectURL(new Blob([bytes],{type:'application/pdf'}));const link=document.createElement('a');link.href=url;link.download='Programa-Auditorias-Internas-'+options.year+'.pdf';link.click();setTimeout(()=>URL.revokeObjectURL(url),30000);}
 root.NuclaudEmail={pdf,download,programPdf,downloadProgram};
})(globalThis);
