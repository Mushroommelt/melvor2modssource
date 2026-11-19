"""Compatibility inspection of user-supplied Melvor 2 Early Access assemblies.
Run with python3 StaticCompatibilityAudit.py <EarlyAccessGameDLL> <MultitaskingDLL> <OldGameDLL>
No game code is executed or modified. Writes a static audit only.
"""
import sys,hashlib
from pathlib import Path
BASE=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(BASE/'tools'))
from cli_inspect import Cli

def methods(c):
    return {c.methodname(i) for i in range(1,len(c.rows.get(6,[]))+1)}

def lookup(method_names,owner,fragment):
    return [m for m in method_names if m.startswith(owner+'.') and fragment in m]

def verify(new,multi,old):
    print('EARLY ACCESS STATIC COMPATIBILITY AUDIT')
    print('Game DLL SHA-256:',hashlib.sha256(Path(new).read_bytes()).hexdigest())
    game,mod,oldgame=Cli(new),Cli(multi),Cli(old)
    ng=methods(game);og=methods(oldgame)
    print('Game method signatures: playtest =',len(og),'early-access =',len(ng))
    print('Methods changed game-wide: removed',len(og-ng),'added',len(ng-og))
    refs=[]; missing=[]
    for i,row in enumerate(mod.rows.get(10,[]),1):
        m=mod.membername(i)
        if m.startswith('GamesByMalcs.Melvor2.'):
            refs.append(m)
            owner,name=m.split('::',1)
            # Method-name and return signatures are kept in the game metadata.
            if '(' in name and not any(v.startswith(owner+'.') and v.endswith(name) for v in ng):
                missing.append(m)
    print('Multitasking direct game-method references:',len(refs),'missing:',len(missing))
    for m in missing:print('MISSING:',m)
    checks={
      'Loader entrypoint':('GodotPlugins.Game.Main','InitializeFromGameProject('),
      'Loader menu':('GamesByMalcs.Melvor2.MainMenu.MainMenuView','_Ready('),
      'Parallel start hook':('GamesByMalcs.Melvor2.Components.Actions.ActionComponent','TryStart('),
      'Stop conflicting':('GamesByMalcs.Melvor2.Components.Actions.ActiveActionManager','TryStopActive('),
      'Recipe selector UI':('GamesByMalcs.Melvor2.Components.ActionUntilX.StopOptionProviderView','Bind('),
      'Recipe goal stop hook':('GamesByMalcs.Melvor2.Components.ActionUntilX.ActionUntilXComponent','StopGoalIsCompleteChangedHandler('),
      'Recipe condition stop hook':('GamesByMalcs.Melvor2.Components.ActionUntilX.ActionUntilXComponent','StopConditionMetHandler('),
      'Recipe manual stop':('GamesByMalcs.Melvor2.Components.Actions.ActionComponent','TryStop('),
      'Game offline request':('GamesByMalcs.Melvor2.Game','RequestOfflineHours('),
      'Run offline replay':('GamesByMalcs.Melvor2.Main+GameState','RunOfflineProgress('),
      'Offline end':('GamesByMalcs.Melvor2.Game','OnOfflineProgressEnd('),
      'Time Skip UI':('GamesByMalcs.Melvor2.GameMenus.GameView','Bind('),
    }
    failed=[]
    for title,(owner,fragment) in checks.items():
        match=lookup(ng,owner,fragment)
        stable=any(v in og for v in match)
        print(('PASS' if match and stable else 'FAIL'),title,('STABLE' if stable else 'CHANGED'), match[0] if match else 'NOT FOUND')
        if not match or not stable:failed.append(title)
    # Most important dynamic pattern in the binary Multitasking transpiler:
    target=lookup(ng,'GamesByMalcs.Melvor2.Components.Actions.ActionComponent','bool TryStart()')
    from ildump import tokenname
    # Resolve the TryStart definition from its owner's MethodDef range, rather than scanning 78K signatures again.
    owner=next(i for i in range(1,len(game.rows[2])+1) if game.fullname(i)=='GamesByMalcs.Melvor2.Components.Actions.ActionComponent')
    rid=next(i for i in range(game.rows[2][owner-1][5],game.rows[2][owner][5]) if 'bool TryStart()' in game.methodsig(i))
    body=game.method_body(rid) if rid else b''
    action_manager=next(i for i in range(1,len(game.rows[2])+1) if game.fullname(i)=='GamesByMalcs.Melvor2.Components.Actions.ActiveActionManager')
    stop_rid=next(i for i in range(game.rows[2][action_manager-1][5],game.rows[2][action_manager][5]) if 'bool TryStopActive(bool)' in game.methodsig(i))
    matched=(b'\x6f'+(0x06000000|stop_rid).to_bytes(4,'little')) in body
    print(('PASS' if matched else 'FAIL'),'Multitasking IL transpiler target: callvirt TryStopActive(bool)')
    if not matched:failed.append('Transpiler target')
    if missing:failed.append('Multitasking references')
    print('STATIC COMPATIBILITY:', 'PASS (runtime untested)' if not failed else 'FAIL: '+', '.join(failed))
    return int(bool(failed))

if __name__=='__main__':
    if len(sys.argv)!=4:
        print('usage: python StaticCompatibilityAudit.py <game.dll> <multitasking.dll> <oldGame.dll>');exit(2)
    sys.exit(verify(*sys.argv[1:]))
