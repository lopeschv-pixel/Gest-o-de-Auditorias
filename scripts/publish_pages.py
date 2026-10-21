"""Update only index.html on gh-pages, preserving its history and other files."""
import os
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parent.parent

def git(*args, input=None, env=None):
    return subprocess.check_output(['git', '-C', str(root), *args], input=input, env=env).decode().strip()

preview = subprocess.check_output(['python', str(root / 'scripts/build_preview.py')], text=True).strip()
subprocess.run(['git', '-C', str(root), 'fetch', 'origin', 'gh-pages:refs/remotes/origin/gh-pages'], check=True)
parent = git('rev-parse', 'refs/remotes/origin/gh-pages')
blob = git('hash-object', '-w', preview)
with tempfile.TemporaryDirectory() as temp:
    env = dict(os.environ, GIT_INDEX_FILE=str(Path(temp) / 'pages-index'))
    git('read-tree', parent, env=env)
    git('update-index', '--add', '--cacheinfo', f'100644,{blob},index.html', env=env)
    tree = git('write-tree', env=env)
source = git('rev-parse', 'HEAD')
commit = git('-c', 'user.name=Codex', '-c', 'user.email=codex@users.noreply.github.com', 'commit-tree', tree, '-p', parent, input=f'Update NUCLAUD demo from {source}\n'.encode())
subprocess.run(['git', '-C', str(root), 'push', 'origin', f'{commit}:refs/heads/gh-pages'], check=True)
print('Updated gh-pages:', commit)
