from pathlib import Path
from hashlib import sha256
import json, re, xml.etree.ElementTree as ET, sys

root=Path(__file__).resolve().parent.parent
checks=[]

def check(name,condition):
    checks.append((name,bool(condition)))
    print(('PASS' if condition else 'FAIL')+' '+name)

def read(rel): return (root/rel).read_text(encoding='utf-8')

check('Target Windows Early Access game SHA is locked',
      '0A5A9FFF320AAAFF781816DEE0EEE938D61D14982BA7F4523DD8CA31D95F27A9' in read('Install-All.ps1'))
check('Loader version is v0.2.5.1',
      '<ModLoaderVersion>0.2.5.1</ModLoaderVersion>' in read('src/ModLoader_EA/Directory.Build.props'))
check('Install banner is v0.2.5.1','v0.2.5.1' in read('Install-All.cmd'))
check('Pasteable folder picker remains','System.Windows.Forms.TextBox' in read('Install-All.ps1'))
check('Build before deployment',
      read('Install-All.ps1').index("Step 'Compile Time Skip'")<read('Install-All.ps1').index("Step 'Install loader and all three mods'"))
check('Rollback script remains referenced','Restore-Backup.ps1' in read('Install-All.ps1'))

for rel in (
 'src/ModLoader_EA/src/ModLoader/MultitaskingActivityHud.cs',
 'src/Melvor2TimeSkip/src/TimeSkipUi.cs',
):
    source=read(rel)
    check('Godot timer fully qualified in '+rel,
          source.count('new Godot.Timer {')==1 and not re.search(r'\bnew\s+Timer\s*\{',source))

for rel in (
 'src/Melvor2RecipeQueue/RecipeQueue.csproj',
 'src/Melvor2TimeSkip/Melvor2TimeSkip.csproj',
 'src/ModLoader_EA/src/ModLoader/ModLoader.csproj',
 'src/ModLoader_EA/src/Launcher/Launcher.csproj',
):
    ET.parse(root/rel)
    check('Valid project XML '+rel,(root/rel).is_file())

for mod in ('Multitasking','Melvor2RecipeQueue','Melvor2TimeSkip'):
    prefix=('mods/'+mod if mod=='Multitasking' else 'src/'+mod)
    meta=json.loads(read(prefix+'/mod.json'))
    check('Valid '+mod+' manifest ID/assembly',meta['id']==mod and meta['assembly'].endswith('.dll'))
    if mod=='Multitasking':
        check('Bundled original Multitasking DLL',(root/prefix/meta['assembly']).is_file())
    else:
        check('Editable C# source '+mod, len(list((root/prefix/'src').glob('*.cs')))>=3)
check('Time Skip manifest identifies hotfix',json.loads(read('src/Melvor2TimeSkip/mod.json'))['version']=='0.2.5.1-ea')
check('Quick buttons still present',all(x in read('src/Melvor2TimeSkip/src/TimeSkipUi.cs')
                                        for x in ('"1m", 1','"5m", 5','"15m", 15','"1h", 60')))
check('Multitasking HUD reads active actions','ActionManager.ActiveActions' in read('src/ModLoader_EA/src/ModLoader/MultitaskingActivityHud.cs'))
check('Original game binaries not bundled',not any(x.name in ('GamesByMalcs.Melvor2.dll','GodotSharp.dll')
                                                for x in root.rglob('*') if x.is_file()))

expected = {'mods/Multitasking/MultitaskingMod.dll': 'a46fd0a514c37c424953833e14b4c3493241bb93f86fe2e1c3fab5638394ebfc', 'mods/Multitasking/mod.json': '82f6cbe0e3a89779b0751c44d0140c3e1adffa9c37e0c05ab07bb2d335a749f7', 'src/Melvor2RecipeQueue/src/QueueController.cs': '80ba36824e5bbfb03c39139c24e440a4e8376cf76a17032df9e2c03ba40017b7', 'src/Melvor2RecipeQueue/src/RecipeQueueMod.cs': 'f1fa0eaa575db4f63c479a2aa0f619c1733e9dcf6a5f8417ae447395bd316048', 'src/Melvor2RecipeQueue/src/QueueUi.cs': 'c4a9a1a72443a0ae62fb35d55c1536bb090066d62a4e286b068922608db716e5', 'src/Melvor2TimeSkip/src/TimeSkipRuntime.cs': 'aeaf94197c7056ab5000ec2c2b57863a92110cb254319b788b5a8ba173a0e16a', 'src/Melvor2TimeSkip/src/TimeSkipMod.cs': '796e2f3bd97c4807ddf6bd83a54b056c7277a40295dc483441199d60406fad8e', 'src/ModLoader_EA/src/Launcher/ModdedGame.cs': '440ae9558c81d7bf31bd826c96a9aaaf7ed73cf9796aa8df3cb1a37279ec2509', 'src/ModLoader_EA/src/Launcher/BootstrapPatcher.cs': '3e2d8adc6a94438dd27eddb10b6f946cae8a674cce903c468b500896e69fd726', 'Install-All.ps1': '9e1728b52467bbc0f0a47c35412c1d331d0d9cb7e30a77a875f9b2a6bf7ab5d8', 'Restore-Backup.ps1': '32060d2c2f96781650d93461d00da99f09766400bb49e47df5adc0b88ddc764e'}
for rel,expected_hash in expected.items():
    file=root/rel
    check('Known-good file unchanged '+rel,
          file.is_file() and sha256(file.read_bytes()).hexdigest()==expected_hash)

passed=sum(ok for _,ok in checks)
print(f'\nSTATIC CHECKS: {passed}/{len(checks)} passed. This does not compile C# or test game runtime.')
sys.exit(0 if passed==len(checks) else 1)
