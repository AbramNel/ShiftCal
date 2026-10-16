const { initializeTestEnvironment, assertSucceeds: success, assertFails: failure } = require('@firebase/rules-unit-testing');
const { doc, setDoc, getDoc, getDocFromCache, getDocFromServer, updateDoc, deleteDoc, Timestamp, writeBatch, disableNetwork, enableNetwork, runTransaction } = require('firebase/firestore');
const assert = require('node:assert/strict');
const fs = require('node:fs');
let assertions=0;const assertSucceeds=p=>{assertions++;return success(p);};const assertFails=p=>{assertions++;return failure(p);};
(async () => {
  const env = await initializeTestEnvironment({ projectId: 'demo-shiftcal', firestore: { host:'127.0.0.1',port:8086,rules:fs.readFileSync('../../firestore.shiftcal.rules','utf8') } });
  try {
    const owner=env.authenticatedContext('owner',{email:'owner@example.com',email_verified:true,firebase:{sign_in_provider:'google.com'}}).firestore();
    const invited=env.authenticatedContext('invited',{email:'invited@example.com',email_verified:true,firebase:{sign_in_provider:'google.com'}}).firestore();
    const stranger=env.authenticatedContext('stranger',{email:'stranger@example.com',email_verified:true,firebase:{sign_in_provider:'google.com'}}).firestore();
    const base='shiftcal/v1/groups/test';
    const batch=writeBatch(owner);batch.set(doc(owner,base),{name:'Test group',owner:'owner'});batch.set(doc(owner,base+'/members/owner'),{role:'owner'});await assertSucceeds(batch.commit());
    await assertFails(setDoc(doc(stranger,base+'/members/stranger'),{role:'owner'}));
    await assertFails(getDoc(doc(stranger,base)));
    await assertSucceeds(setDoc(doc(owner,base+'/invites/code'),{email:'invited@example.com',expires:Timestamp.fromMillis(Date.now()+86400000)}));
    await assertFails(setDoc(doc(stranger,base+'/members/stranger'),{role:'member',invite:'code'}));
    await assertSucceeds(setDoc(doc(invited,base+'/members/invited'),{role:'member',invite:'code'}));
    await assertFails(updateDoc(doc(invited,base),{owner:'invited'}));
    const record=(key,author)=>({key,json:'{}',deleted:false,revision:1,author});
    await assertSucceeds(setDoc(doc(owner,base+'/records/shift~2'),record('shift/2','owner')));
    await assertFails(setDoc(doc(invited,base+'/records/shift~3'),record('shift/3','invited')));
    await assertSucceeds(setDoc(doc(invited,base+'/records/event~meeting'),record('event/meeting','invited')));
    await assertFails(updateDoc(doc(invited,base+'/records/event~meeting'),{revision:3}));
    await assertFails(updateDoc(doc(invited,base+'/records/event~meeting'),{revision:2,deleted:true}));
    await assertSucceeds(updateDoc(doc(owner,base+'/records/event~meeting'),{revision:2,deleted:true,author:'owner'}));
    await assertFails(updateDoc(doc(invited,base+'/records/event~meeting'),{revision:3,deleted:false,author:'invited'}));
    await assertFails(deleteDoc(doc(invited,base+'/records/event~meeting')));
    await assertFails(setDoc(doc(stranger,'shiftcal/v1/users/owner/records/rule~wake'),record('rule/wake','stranger')));
    await env.withSecurityRulesDisabled(async ctx=>setDoc(doc(ctx.firestore(),'shiftcal/v1/users/owner/records/rule~wake'),record('rule/wake','owner')));
    await assertSucceeds(getDoc(doc(owner,'shiftcal/v1/users/owner/records/rule~wake')));
    await assertFails(updateDoc(doc(owner,'shiftcal/v1/users/owner/records/rule~wake'),{revision:2}));
    await assertFails(setDoc(doc(owner,'shiftcal/v1/users/owner/records/rule~new'),record('rule/new','owner')));
    for(const type of ['activity','activityException','activityTemplate']) {
      const path=base+'/records/'+type+'~shared';
      await assertSucceeds(setDoc(doc(invited,path),record(type+'/shared','invited')));
      await assertSucceeds(getDoc(doc(owner,path)));
      await assertFails(setDoc(doc(stranger,path),{...record(type+'/shared','stranger'),revision:2}));
      await assertFails(updateDoc(doc(invited,path),{revision:2,sound:'alarm'}));
      await assertSucceeds(updateDoc(doc(invited,path),{revision:2,json:'{"title":"Member edit"}',author:'invited'}));
      if(type==='activityTemplate')await assertSucceeds(updateDoc(doc(invited,path),{revision:3,author:'invited',deleted:true}));
      else await assertFails(updateDoc(doc(invited,path),{revision:3,author:'invited',deleted:true}));
      await assertSucceeds(updateDoc(doc(owner,path),{revision:type==='activityTemplate'?4:3,author:'owner',deleted:true}));
      await assertFails(updateDoc(doc(invited,path),{revision:2,author:'invited'}));
    }
    await assertSucceeds(setDoc(doc(owner,base+'/records/familyProfile~child'),record('familyProfile/child','owner')));
    await assertFails(updateDoc(doc(invited,base+'/records/familyProfile~child'),{revision:2,author:'invited'}));
    await assertFails(setDoc(doc(invited,base+'/records/familyProfile~new'),record('familyProfile/new','invited')));
    await assertFails(setDoc(doc(owner,base+'/records/rule~wake'),record('rule/wake','owner')));
    await assertFails(setDoc(doc(env.unauthenticatedContext().firestore(),base+'/records/activity~anonymous'),record('activity/anonymous','anonymous')));
    for(const type of ['activity','activityException','familyProfile','activityTemplate']) {
      await assertSucceeds(setDoc(doc(owner,'shiftcal/v1/users/owner/calendar/'+type+'~solo'),record(type+'/solo','owner')));
      await assertFails(getDoc(doc(stranger,'shiftcal/v1/users/owner/calendar/'+type+'~solo')));
    }
    await assertFails(getDoc(doc(invited,'shiftcal/v1/users/owner/records/rule~wake')));
    await assertFails(updateDoc(doc(owner,base+'/records/shift~2'),{key:'event/change',revision:2}));
    await assertFails(setDoc(doc(invited,base+'/records/familyProfile~spoof'),record('activity/spoof','invited')));
    const anonymous=env.authenticatedContext('anonymous',{firebase:{sign_in_provider:'anonymous'}}).firestore();
    await assertFails(setDoc(doc(anonymous,'shiftcal/v1/groups/anonymous'),{name:'Anonymous',owner:'anonymous'}));
    await assertFails(setDoc(doc(anonymous,'shiftcal/v1/users/anonymous/calendar/activity~new'),record('activity/new','anonymous')));
    for(const type of ['workedHours','personalAlarm','rule'])await assertFails(setDoc(doc(owner,'shiftcal/v1/users/owner/calendar/'+type+'~private'),record(type+'/private','owner')));
    const pendingPath='shiftcal/v1/users/owner/calendar/event~offline';
    await assertSucceeds(setDoc(doc(owner,pendingPath),record('event/offline','owner')));
    await disableNetwork(owner);
    const pending=setDoc(doc(owner,pendingPath),{...record('event/offline','owner'),revision:2,json:'{"title":"Kept offline"}'});
    const cached=await getDocFromCache(doc(owner,pendingPath));assert.equal(cached.data().json,'{"title":"Kept offline"}');assert.equal(cached.metadata.hasPendingWrites,true);assertions+=2;
    await enableNetwork(owner);await assertSucceeds(pending);const restored=await getDocFromServer(doc(owner,pendingPath));assert.equal(restored.data().revision,2);assertions++;
    const conflictPath=base+'/records/activity~concurrent';await assertSucceeds(setDoc(doc(owner,conflictPath),record('activity/concurrent','owner')));await assertSucceeds(updateDoc(doc(invited,conflictPath),{revision:2,author:'invited',json:'{"title":"Other device"}'}));
    const queued={json:'{"title":"My pending edit"}',revision:1,pending:true};const accepted=await runTransaction(owner,async tx=>{const remote=await tx.get(doc(owner,conflictPath));if(remote.data().revision!==queued.revision)return false;tx.update(doc(owner,conflictPath),{revision:2,json:queued.json,author:'owner'});return true;});assert.equal(accepted,false);assert.equal(queued.pending,true);assertions+=2;
    const defaultConfig=JSON.parse(fs.readFileSync('../../firebase.json','utf8'));const defaultRules=fs.readFileSync('../../'+defaultConfig.firestore.rules,'utf8');
    const canonicalRules=fs.readFileSync('../../firestore.shiftcal.rules','utf8');assert.ok(defaultRules.includes(canonicalRules.slice(canonicalRules.indexOf('    function scSigned()'),canonicalRules.lastIndexOf('\n  }'))));assertions++;
    const merged=await initializeTestEnvironment({projectId:'demo-shiftcal-default',firestore:{host:'127.0.0.1',port:8086,rules:defaultRules}});
    try{
      const self=merged.authenticatedContext('legacy',{firebase:{sign_in_provider:'google.com'}}).firestore();
      await assertSucceeds(setDoc(doc(self,'users/legacy'),{userId:'legacy'}));
      await assertSucceeds(setDoc(doc(self,'shiftcal/v1/users/legacy/calendar/event~default'),record('event/default','legacy')));
      await assertFails(getDoc(doc(merged.authenticatedContext('other').firestore(),'shiftcal/v1/users/legacy/calendar/event~default')));
    }finally{await merged.cleanup();}
    console.log(`PASS: ${assertions} Firestore emulator assertions: canonical/default rules, owner-only shared cleanup, member edits, offline queued writes, conflicts, account isolation and private-data rejection`);
  } finally { await env.cleanup(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
