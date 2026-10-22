"""Build an offline, single-file preview from the same application source."""
from pathlib import Path
root = Path(__file__).resolve().parent.parent
static = root / 'static'
html = (static / 'index.html').read_text()
css = (static / 'style.css').read_text()
css = css[css.index(':root'):]
html = html.replace('<link rel="stylesheet" href="/style.css">', '<style>' + css + '</style>')
theme=(static/'theme.css').read_text()
hero=static/'assets'/'nuclaud-hero.png'
if hero.exists():
    import base64
    theme=theme.replace('/assets/nuclaud-hero.png','data:image/png;base64,'+base64.b64encode(hero.read_bytes()).decode())
html=html.replace('<link rel="stylesheet" href="/theme.css">','<style>'+theme+'</style>')
catalog = (static / 'requirements-catalog.json').read_text()
html = html.replace('<script src="/planning.js"></script>', '<script>globalThis.NUCLAUD_REQUIREMENT_CATALOG=' + catalog + ';</script><script src="/planning.js"></script>')
html = html.replace('<script src="/access-policy.js"></script>', '<script>' + (static / 'access-policy.js').read_text() + '</script>')
html = html.replace('<script src="/planning.js"></script>', '<script>' + (static / 'planning.js').read_text() + '</script>')
html = html.replace('<script src="/reporting.js"></script>', '<script>' + (static / 'reporting.js').read_text() + '</script>')
for script in ['vendor/jspdf.umd.min.js','email.js']:
    html=html.replace(f'<script src="/{script}"></script>', '<script>'+(static/script).read_text()+'</script>')
html = html.replace('<script src="/app.js"></script>', '<script>window.NUCLAUD_PREVIEW=true;</script><script>' + (static / 'app.js').read_text() + '</script>')
html = html.replace('href="/"', 'href="#"').replace('Ambiente de desenvolvimento', 'Prévia local · dados neste navegador')
output = Path('/tmp/nuclaud-preview/NUCLAUD-v12.html')
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(html)
print(output)
