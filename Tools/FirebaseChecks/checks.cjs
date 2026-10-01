const { initializeTestEnvironment, assertSucceeds, assertFails } = require('@firebase/rules-unit-testing');
const { doc, setDoc, getDoc, updateDoc, deleteDoc, Timestamp, writeBatch } = require('firebase/firestore');
const fs = require('node:fs');
(async () => {
  const env = await initializeTestEnvironment({ projectId: 'demo-shiftcal', firestore: { host:'127.0.0.1',port:8086,rules:fs.readFileSync('../../firestore.shiftcal.rules','utf8') } });
  try {
    const owner=env.authenticatedContext('owner',{email:'owner@example.com',email_verified:true}).firestore();
    const invited=env.authenticatedContext('invited',{email:'invited@example.com',email_verified:true}).firestore();
    const stranger=env.authenticatedContext('stranger',{email:'stranger@example.com',email_verified:true}).firestore();
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
    await assertSucceeds(setDoc(doc(owner,'shiftcal/v1/users/owner/records/rule~wake'),record('rule/wake','owner')));
    await assertFails(getDoc(doc(invited,'shiftcal/v1/users/owner/records/rule~wake')));
    await assertFails(updateDoc(doc(owner,base+'/records/shift~2'),{key:'event/change',revision:2}));
    console.log('PASS: 17 Firestore membership, ownership, private rules, revisions and tombstone checks');
  } finally { await env.cleanup(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
