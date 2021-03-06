﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using InboxWatcher.ImapClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InboxWatcher.Interface;
using MailKit;
using Moq;

namespace InboxWatcher.ImapClient.Tests
{
    [TestClass()]
    public class ImapIdlerTests
    {
        private Mock<ImapClientConfiguration> _configuration;
        private Mock<IImapClient> client;
        private Mock<IMailFolder> inbox;
        private Mock<IImapFactory> factory;
        private IImapIdler imapIdler;

        [TestInitialize]
        public void TestInitialize()
        {
            _configuration = new Mock<ImapClientConfiguration>();

            //mock client
            client = new Mock<IImapClient>();

            //mock inbox folder
            inbox = new Mock<IMailFolder>();

            //client returns mock inbox
            client.Setup(x => x.Inbox).Returns(inbox.Object);

            //mock factory
            factory = new Mock<IImapFactory>();
            factory.Setup(x => x.GetClient()).ReturnsAsync(client.Object);

            //create the idler and start setup
            imapIdler = new ImapIdler(factory.Object);
        }

        [TestMethod()]
        public void ImapIdlerConstructorTest()
        {
            var factory = new Mock<ImapClientFactory>(_configuration.Object);
            var idler = new ImapIdler(factory.Object);

            var imapIdlerPvtObject = new PrivateObject(idler);

            Assert.AreEqual(factory.Object, imapIdlerPvtObject.GetFieldOrProperty("Factory"));
            Assert.AreNotEqual(null, imapIdlerPvtObject.GetFieldOrProperty("Timeout"));
            Assert.AreNotEqual(null, imapIdlerPvtObject.GetFieldOrProperty("IntegrityCheckTimer"));
        }


        [TestMethod()]
        public void SetupTest()
        {
            //mock trace listener
            var trace = new Mock<TraceListener>();
            Trace.Listeners.Add(trace.Object);

            imapIdler.Setup(false).Wait();

            //verify the factory was asked for the client
            factory.Verify(x => x.GetClient());

            //verify setup events working - disconnected
            client.Raise(x => x.Disconnected += null, new EventArgs());
            trace.Verify(x => x.WriteLine("ImapClient disconnected"));

            //verify setup events working - inbox opened
            inbox.Raise(x => x.Opened += null, new EventArgs());
            trace.Verify(x => x.WriteLine(It.Is<string>(z => z.Contains("Inbox opened"))));

            //verify setup events working - inbox closed
            inbox.Raise(x => x.Closed += null, new EventArgs());
            trace.Verify(x => x.WriteLine(It.Is<string>(z => z.Contains("Inbox closed"))));
        }

        
        [TestMethod()]
        public void StartIdlingTest()
        {
            imapIdler.Setup(false).Wait();

            //verify that inbox was opened and client idled
            inbox.Verify(x => x.OpenAsync(It.IsAny<FolderAccess>(), It.IsAny<CancellationToken>()));
            client.Verify(x => x.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()));
        }

        [TestMethod()]
        public void StartIdlingTestWithException()
        {
            //simulate an idling client that suddenly disconnects
            client.Setup(x => x.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
                .Returns(Task.Run(async () =>
                {
                    //idle for 1 second and then throw exception
                    await Task.Delay(1000);
                    throw new ServiceNotConnectedException("client disconnected");
                })).Callback(() => //after the first failure things clear up and the client is working again
                {
                    client.Setup(x => x.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);
                });
            
            //setup imapIdler
            imapIdler.Setup().Wait();

            //verify that inbox was opened and client idled
            inbox.Verify(x => x.OpenAsync(It.IsAny<FolderAccess>(), It.IsAny<CancellationToken>()));
            client.Verify(x => x.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()));

            //get the private idle task from idler
            var pvt = new PrivateObject(imapIdler);
            var idleTask = (Task) pvt.GetFieldOrProperty("IdleTask");
            
            //the task is running and hasn't faulted yet
            Assert.AreNotEqual(null, idleTask);
            Assert.IsFalse(idleTask.IsCompleted);
            Assert.IsFalse(idleTask.IsFaulted);

            //wait for the exception to be thrown
            idleTask.Wait();

            //verify that the setup method was called and a new client is fetched
            factory.Verify(x => x.GetClient(), Times.Exactly(2));
        }

        [TestMethod()]
        public void IdleLoopTest()
        {
            client.Setup(x => x.IsConnected).Returns(true);
            client.Setup(x => x.IsAuthenticated).Returns(true);
            inbox.Setup(x => x.IsOpen).Returns(false);

            var pvt = new PrivateObject(imapIdler);

            imapIdler.Setup(false).Wait();
            pvt.Invoke("IdleLoop", new object[] {null, null});

            inbox.Verify(x => x.OpenAsync(It.IsAny<FolderAccess>(), It.IsAny<CancellationToken>()));
            client.Verify(x => x.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [TestMethod()]
        public void StopIdleTest()
        {
            imapIdler.Setup(false).Wait();

            var pvt = new PrivateObject(imapIdler);

            //return true for client being idle
            client.Setup(x => x.IsIdle).Returns(true);

            //invoke stopidle
            pvt.Invoke("StopIdle", new object[] {"test"});

            var doneToken = (CancellationTokenSource) pvt.GetFieldOrProperty("DoneToken");

            Assert.IsTrue(doneToken.IsCancellationRequested);

            //verify inbox was closed and re-opened
            inbox.Verify(x => x.CloseAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()));
            inbox.Verify(x => x.OpenAsync(It.IsAny<FolderAccess>(), It.IsAny<CancellationToken>()));
        }

        [TestMethod]
        public void StopIdleTestWithException()
        {
            imapIdler.Setup(false).Wait();
            var pvt = new PrivateObject(imapIdler);


        }
    }
}