const { initializeTestEnvironment, assertSucceeds: success, assertFails: failure } = require('@firebase/rules-unit-testing');
const { doc, setDoc, getDoc, updateDoc, deleteDoc, Timestamp, writeBatch } = require('firebase/firestore');
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
    await assertSucceeds(updateDoc(doc(invited,base+'/records/event~meeting'),{revision:2,deleted:true}));
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
      await assertSucceeds(updateDoc(doc(owner,path),{revision:2,author:'owner',deleted:true}));
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
    console.log(`PASS: ${assertions} Firestore emulator assertions: family records, owner-only profiles, account isolation, no alarm writes, anonymous/outsider denial, revisions and tombstones`);
  } finally { await env.cleanup(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
