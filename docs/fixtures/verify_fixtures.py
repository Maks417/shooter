"""Read-only specification check; standard library, Python 3.10+. Not game tests."""
from pathlib import Path
import hashlib
import json
import math
import re
import struct

ROOT = Path(__file__).resolve().parent

def pairs(values):
    out = {}
    for key,value in values:
        if key in out:
            raise ValueError('Duplicate JSON key: '+key)
        out[key] = value
    return out

def read(name):
    return json.loads((ROOT/name).read_text(encoding='utf-8'),object_pairs_hook=pairs,
                      parse_constant=lambda s: (_ for _ in ()).throw(ValueError(s)))

def check_schema(value, rule, path='$'):
    """Only keywords used in delivered schemas. Not a general JSON Schema engine."""
    errors=[]
    def fail(message): errors.append(path+': '+message)
    if 'const' in rule and value != rule['const']: fail('const')
    if 'enum' in rule and value not in rule['enum']: fail('enum')
    kind=rule.get('type')
    kinds=kind if isinstance(kind,list) else [kind] if kind else []
    def is_type(t):
        return {'object':isinstance(value,dict),'array':isinstance(value,list),
          'string':isinstance(value,str),'boolean':isinstance(value,bool),
          'integer':isinstance(value,int) and not isinstance(value,bool),
          'number':isinstance(value,(int,float)) and not isinstance(value,bool),
          'null':value is None}.get(t,False)
    if kinds and not any(is_type(t) for t in kinds):
        fail('type');return errors
    if isinstance(value,dict):
        for key in rule.get('required',[]):
            if key not in value:fail('missing '+key)
        props=rule.get('properties',{})
        for key,v in value.items():
            if key in props:errors+=check_schema(v,props[key],path+'.'+key)
            elif rule.get('additionalProperties') is False:fail('extra '+key)
    if isinstance(value,list):
        if len(value)<rule.get('minItems',0):fail('minItems')
        if len(value)>rule.get('maxItems',10**9):fail('maxItems')
        if rule.get('uniqueItems') and len({json.dumps(x,sort_keys=True) for x in value})!=len(value):fail('uniqueItems')
        for i,v in enumerate(value):errors+=check_schema(v,rule.get('items',{}),path+f'[{i}]')
    if isinstance(value,str) and 'pattern' in rule and not re.search(rule['pattern'],value):fail('pattern')
    if isinstance(value,(int,float)) and not isinstance(value,bool):
        if not math.isfinite(value):fail('nonfinite')
        if 'minimum' in rule and value<rule['minimum']:fail('minimum')
        if 'maximum' in rule and value>rule['maximum']:fail('maximum')
        if 'exclusiveMaximum' in rule and value>=rule['exclusiveMaximum']:fail('exclusiveMaximum')
    return errors

def lp(value):
    if isinstance(value,str): value=value.encode('utf-8')
    return struct.pack('<I',len(value))+value

def sha(value): return hashlib.sha256(value).hexdigest()
def canonical(value):return json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=True).encode('utf-8')

def pcg_outputs(state,sequence,count):
    s=0;inc=((sequence<<1)|1)&((1<<64)-1)
    def next_value():
        nonlocal s
        old=s;s=(old*6364136223846793005+inc)&((1<<64)-1)
        x=(((old>>18)^old)>>27)&0xffffffff;rot=old>>59
        return ((x>>rot)|(x<<((-rot)&31)))&0xffffffff
    next_value();s=(s+state)&((1<<64)-1);next_value()
    return [f'{next_value():08x}' for _ in range(count)]

def content_key(args):
    b=lp('shooter.asset.v1')+lp(bytes.fromhex(args['sourceHash']))
    b+=lp(args['generatorId'])+lp(args['generatorVersion'])+struct.pack('<Q',int(args['seed']))
    b+=struct.pack('<I',len(args['dependencies']))
    b+=b''.join(lp(bytes.fromhex(x)) for x in sorted(args['dependencies']))
    b+=lp(args['targetPlatform'])+lp(args['visualProfile'])+struct.pack('<I',args['formatVersion'])+lp(bytes.fromhex(args['toolchainHash']))
    return sha(b)

def main():
    assertions=0
    def require(ok,message):
        nonlocal assertions
        assertions+=1
        if not ok:raise AssertionError(message)
    fixtures=list(ROOT.glob('*.json'))
    for p in fixtures:read(p.name)
    for fixture,sch in [('box-recipe.json','recipe.schema.json'),('command.json','command.schema.json'),('manifest.json','manifest.schema.json'),('benchmark-result.json','benchmark-result.schema.json')]:
        errors=check_schema(read(fixture),read(sch));require(not errors,f'{fixture}: {errors}')
    for fixture,sch in [('invalid-box-recipe.json','recipe.schema.json'),('invalid-command.json','command.schema.json')]:
        require(bool(check_schema(read(fixture),read(sch))),fixture+' should reject')
    g=read('golden-vectors.json');r=read('box-recipe.json')
    require(pcg_outputs(int(g['pcg']['initstate']),int(g['pcg']['initseq']),6)==g['pcg']['outputsHex'],'PCG vector mismatch')
    require(canonical(r).decode()==g['canonicalUtf8'],'canonical bytes')
    require(sha(canonical(r))==g['canonicalSha256'],'canonical digest')
    require(sha(lp(r['schemaId'])+lp(canonical(r)))==g['sourceHash'],'source hash')
    require(content_key(g['contentKeyInputs'])==g['contentId'],'content key')
    stream=g['namedStream'];b=lp(stream['domain'])+struct.pack('<Q',int(stream['rootSeed']))+lp(stream['recipeId'])+lp(stream['featureKey'])
    require(b.hex()==stream['bytesHex'] and sha(b)==stream['sha256'],'stream bytes')
    dig=hashlib.sha256(b).digest()
    require(str(int.from_bytes(dig[:8],'little'))==stream['initstate'],'stream state')
    require(str(int.from_bytes(dig[8:16],'little'))==stream['initseq'],'stream sequence')
    require(sha(lp('shooter.unity-guid.v1')+lp(g['logicalAssetId']))[:32]==g['unityGuid'],'stable GUID')
    m=read('manifest.json');require(m['contentId']==g['contentId'],'manifest content ID')
    require('generationDurationMs' not in m and 'timestamp' not in m,'telemetry in immutable manifest')
    for a in m['artifacts']:
        p=(ROOT/a['path']).resolve();require(p.is_relative_to(ROOT),'escaping artifact')
        payload=p.read_bytes();require(sha(payload)==a['sha256'],'payload hash')
        require(len(payload)==a['uncompressedBytes']==a['compressedBytes'],'payload lengths')
    command=read('command.json');require(0<int(command['entityId'])<2**64,'entity range')
    require(command['moveX']**2+command['moveY']**2<=1,'movement normalization')
    require(command['buttons']&~127==0,'unknown buttons')
    snapshot=read('snapshot.json');ids=[x['id'] for x in snapshot['entities']]
    require(len(ids)==len(set(ids)),'snapshot duplicate actors')
    gameplay=read('gameplay-defaults.json');require(gameplay['weapons']['rifle']['fireTicks']==6,'rifle interval')
    require(math.floor(30*.75+.5)==23 and math.floor(25*.75+.5)==19,'damage rounding')
    workloads=read('benchmark-workloads.json');b03=next(x for x in workloads['workloads'] if x['id']=='B03-combat-v1')
    count=sum(t%b03['playerFireEveryTicks']==0 for t in range(workloads['warmupTicks'],workloads['warmupTicks']+workloads['captureTicks']))
    require(count==b03['expectedCaptureShots']==1800,'shot schedule')
    require(b03['benchmarkMagazine']==65535,'benchmark ammo cannot exhaust')
    require(count+600<65535,'warmup/capture ammo capacity')
    result=read('benchmark-result.json');require(result['gateStatus']=='PendingHardware','fixture must not imply measurement')
    require(all(x is None for x in result['metrics']['frameMs'].values()),'fabricated timing')
    require(len(read('prediction-state.json')['weaponSlots'])==2,'prediction slots')
    # Mutation tests independently exercise the subset validator.
    require(bool(check_schema(dict(command,unexpected=1),read('command.schema.json'))),'extra field acceptance')
    require(bool(check_schema(dict(command,selectedSlot=2),read('command.schema.json'))),'invalid enum acceptance')
    require(bool(check_schema(dict(r,seed='01'),read('recipe.schema.json'))),'noncanonical seed')
    print(json.dumps({'result':'PASS','jsonFilesParsed':len(fixtures),'assertions':assertions,
      'scope':'Specification fixtures and supported schema keyword subset. Not C#/Unity/gameplay/hardware tests.'},indent=2))

if __name__=='__main__': main()
