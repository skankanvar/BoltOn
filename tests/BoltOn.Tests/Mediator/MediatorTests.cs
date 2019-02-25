﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using BoltOn.Bootstrapping;
using BoltOn.Logging;
using BoltOn.Mediator.Interceptors;
using BoltOn.Mediator.Pipeline;
using BoltOn.UoW;
using BoltOn.Utilities;
using Moq;
using Moq.AutoMock;
using Xunit;

namespace BoltOn.Tests.Mediator
{
	public class MediatorTests : IDisposable
	{
		[Fact]
		public void Get_RegisteredHandlerThatReturnsBool_ReturnsSuccessfulResult()
		{
			// arrange
			var autoMocker = new AutoMocker();
			var serviceProvider = autoMocker.GetMock<IServiceProvider>();
			var testHandler = new Mock<TestHandler>();
			serviceProvider.Setup(s => s.GetService(typeof(IRequestHandler<TestRequest, bool>)))
						  .Returns(testHandler.Object);
			autoMocker.Use<IEnumerable<IInterceptor>>(new List<IInterceptor>());
			var sut = autoMocker.CreateInstance<BoltOn.Mediator.Pipeline.Mediator>();
			var request = new TestRequest();
			testHandler.Setup(s => s.Handle(request)).Returns(true);

			// act
			var result = sut.Get(request);

			// assert 
			Assert.True(result.IsSuccessful);
			Assert.True(result.Data);
		}

		[Fact]
		public void Get_MediatorWithInterceptor_ExecutesInterceptor()
		{
			// arrange
			var autoMocker = new AutoMocker();
			var serviceProvider = autoMocker.GetMock<IServiceProvider>();
			var testHandler = new Mock<TestHandler>();
			var interceptor = new Mock<IInterceptor>();
			var logger = new Mock<IBoltOnLogger<TestInterceptor>>();
			serviceProvider.Setup(s => s.GetService(typeof(IRequestHandler<TestRequest, bool>)))
						  .Returns(testHandler.Object);
			autoMocker.Use<IEnumerable<IInterceptor>>(new List<IInterceptor>() { new TestInterceptor(logger.Object) });
			var sut = autoMocker.CreateInstance<BoltOn.Mediator.Pipeline.Mediator>();
			var request = new TestRequest();
			testHandler.Setup(s => s.Handle(request)).Returns(true);

			// act
			var result = sut.Get(request);

			// assert 
			Assert.True(result.IsSuccessful);
			Assert.True(result.Data);
			logger.Verify(l => l.Debug("TestInterceptor Started"));
			logger.Verify(l => l.Debug("TestInterceptor Ended"));
		}

		[Fact]
		public void Get_MediatorWithRequestSpecificInterceptor_ExecutesInterceptor()
		{
			// arrange
			var autoMocker = new AutoMocker();
			var serviceProvider = autoMocker.GetMock<IServiceProvider>();
			var testHandler = new Mock<TestHandler>();
			var interceptor = new Mock<IInterceptor>();
			var logger = new Mock<IBoltOnLogger<TestInterceptor>>();
			var logger2 = new Mock<IBoltOnLogger<StopwatchInterceptor>>();
			var boltOnClock = new Mock<IBoltOnClock>();
			var currentDateTime = DateTime.Now;
			boltOnClock.Setup(s => s.Now).Returns(currentDateTime);
			serviceProvider.Setup(s => s.GetService(typeof(IRequestHandler<TestRequest, bool>)))
				.Returns(testHandler.Object);
			autoMocker.Use<IEnumerable<IInterceptor>>(new List<IInterceptor> { new TestRequestSpecificInterceptor(logger.Object),
				new StopwatchInterceptor(logger2.Object, boltOnClock.Object) });
			var sut = autoMocker.CreateInstance<BoltOn.Mediator.Pipeline.Mediator>();
			var request = new TestRequest();
			testHandler.Setup(s => s.Handle(request)).Returns(true);

			// act
			var result = sut.Get(request);

			// assert 
			Assert.True(result.IsSuccessful);
			Assert.True(result.Data);
			logger.Verify(l => l.Debug("TestRequestSpecificInterceptor Started"), Times.Never);
			logger.Verify(l => l.Debug("TestRequestSpecificInterceptor Ended"), Times.Never);
			logger2.Verify(l => l.Debug($"StopwatchInterceptor started at {currentDateTime}"), Times.Once);
		}

		[Fact]
		public void Get_MediatorWithCommandRequest_ExecutesUoWInterceptorAndStartsTransactionsWithDefaultCommandIsolationLevel()
		{
			// arrange
			var autoMocker = new AutoMocker();
			var serviceProvider = autoMocker.GetMock<IServiceProvider>();
			var testHandler = new Mock<TestHandler>();
			var interceptor = new Mock<IInterceptor>();
			var logger = new Mock<IBoltOnLogger<UnitOfWorkInterceptor>>();
			serviceProvider.Setup(s => s.GetService(typeof(IRequestHandler<TestCommand, bool>)))
				.Returns(testHandler.Object);
			var uowManager = autoMocker.GetMock<IUnitOfWorkManager>();
			var uow = new Mock<IUnitOfWork>();
			uowManager.Setup(u => u.Get(It.IsAny<UnitOfWorkOptions>())).Returns(uow.Object);
			var uowOptions = autoMocker.GetMock<UnitOfWorkOptions>();
			uowOptions.Setup(u => u.IsolationLevel).Returns(IsolationLevel.ReadCommitted);
			var uowOptionsBuilder = autoMocker.GetMock<IUnitOfWorkOptionsBuilder>();
			var request = new TestCommand();
			uowOptionsBuilder.Setup(u => u.Build(request)).Returns(uowOptions.Object);
			autoMocker.Use<IEnumerable<IInterceptor>>(new List<IInterceptor>
			{
				new UnitOfWorkInterceptor(logger.Object, uowManager.Object, uowOptionsBuilder.Object)
			});
			var sut = autoMocker.CreateInstance<BoltOn.Mediator.Pipeline.Mediator>();
			testHandler.Setup(s => s.Handle(request)).Returns(true);

			// act
			var result = sut.Get(request);

			// assert 
			Assert.True(result.IsSuccessful);
			Assert.True(result.Data);
			uowManager.Verify(u => u.Get(uowOptions.Object));
			uow.Verify(u => u.Commit());
			logger.Verify(l => l.Debug($"About to start UoW with IsolationLevel: {IsolationLevel.ReadCommitted.ToString()}"));
			logger.Verify(l => l.Debug("UnitOfWorkInterceptor ended"));
   		}

		[Fact]
		public void Get_MediatorWithCommandRequestAndHandlerThrowsException_ExecutesUoWInterceptorAndStartsTransactionsButNotCommit()
		{
			// arrange
			var autoMocker = new AutoMocker();
			var serviceProvider = autoMocker.GetMock<IServiceProvider>();
			var testHandler = new Mock<TestHandler>();
			var interceptor = new Mock<IInterceptor>();
			var logger = new Mock<IBoltOnLogger<UnitOfWorkInterceptor>>();
			serviceProvider.Setup(s => s.GetService(typeof(IRequestHandler<TestCommand, bool>)))
				.Returns(testHandler.Object);
			var uowManager = autoMocker.GetMock<IUnitOfWorkManager>();
			var uow = new Mock<IUnitOfWork>();
			uowManager.Setup(u => u.Get(It.IsAny<UnitOfWorkOptions>())).Returns(uow.Object);
			var uowOptions = autoMocker.GetMock<UnitOfWorkOptions>();
			uowOptions.Setup(u => u.IsolationLevel).Returns(IsolationLevel.ReadCommitted);
			var uowOptionsBuilder = autoMocker.GetMock<IUnitOfWorkOptionsBuilder>();
			var request = new TestCommand();
			uowOptionsBuilder.Setup(u => u.Build(request)).Returns(uowOptions.Object);
			autoMocker.Use<IEnumerable<IInterceptor>>(new List<IInterceptor>
			{
				new UnitOfWorkInterceptor(logger.Object, uowManager.Object, uowOptionsBuilder.Object)
			});
			var sut = autoMocker.CreateInstance<BoltOn.Mediator.Pipeline.Mediator>();
			testHandler.Setup(s => s.Handle(request)).Throws<Exception>();

			// act & assert 
			Assert.Throws<Exception>(() => sut.Get(request));
			uowManager.Verify(u => u.Get(uowOptions.Object));
			uow.Verify(u => u.Commit(), Times.Never);
			logger.Verify(l => l.Debug($"About to start UoW with IsolationLevel: {IsolationLevel.ReadCommitted.ToString()}"));
			logger.Verify(l => l.Debug("UnitOfWorkInterceptor ended"), Times.Never);
		}

		[Fact]
		public async Task Get_MediatorWithAsyncHandlerThrowsException_ExecutesUoWInterceptorAndStartsTransactionsButNotCommit()
		{
			// arrange
			var autoMocker = new AutoMocker();
			var serviceProvider = autoMocker.GetMock<IServiceProvider>();
			var testHandler = new Mock<TestHandler>();
			var interceptor = new Mock<IInterceptor>();
			var logger = new Mock<IBoltOnLogger<UnitOfWorkInterceptor>>();
			serviceProvider.Setup(s => s.GetService(typeof(IRequestAsyncHandler<TestCommand, bool>)))
				.Returns(testHandler.Object);
			var uowManager = autoMocker.GetMock<IUnitOfWorkManager>();
			var uow = new Mock<IUnitOfWork>();
			uowManager.Setup(u => u.Get(It.IsAny<UnitOfWorkOptions>())).Returns(uow.Object);
			var uowOptions = autoMocker.GetMock<UnitOfWorkOptions>();
			uowOptions.Setup(u => u.IsolationLevel).Returns(IsolationLevel.ReadCommitted);
			var uowOptionsBuilder = autoMocker.GetMock<IUnitOfWorkOptionsBuilder>();
			var request = new TestCommand();
			uowOptionsBuilder.Setup(u => u.Build(request)).Returns(uowOptions.Object);
			autoMocker.Use<IEnumerable<IInterceptor>>(new List<IInterceptor>
			{
				new UnitOfWorkInterceptor(logger.Object, uowManager.Object, uowOptionsBuilder.Object)
			});
			var sut = autoMocker.CreateInstance<BoltOn.Mediator.Pipeline.Mediator>();
			testHandler.Setup(s => s.HandleAsync(request, default(CancellationToken))).Throws<Exception>();

			// act 
			var result = await Record.ExceptionAsync(async () => await sut.GetAsync(request));

			//assert 
			uowManager.Verify(u => u.Get(uowOptions.Object));
			uow.Verify(u => u.Commit(), Times.Never);
			logger.Verify(l => l.Debug($"About to start UoW with IsolationLevel: {IsolationLevel.ReadCommitted.ToString()}"));
			logger.Verify(l => l.Debug("UnitOfWorkInterceptor ended"), Times.Never);
		}

		[Fact]
		public void Get_RegisteredHandlerThatThrowsException_ReturnsUnsuccessfulResult()
		{
			// arrange
			var autoMocker = new AutoMocker();
			var serviceProvider = autoMocker.GetMock<IServiceProvider>();
			var testHandler = new Mock<TestHandler>();
			serviceProvider.Setup(s => s.GetService(typeof(IRequestHandler<TestRequest, bool>)))
						   .Returns(testHandler.Object);
			autoMocker.Use<IEnumerable<IInterceptor>>(new List<IInterceptor>());
			var sut = autoMocker.CreateInstance<BoltOn.Mediator.Pipeline.Mediator>();
			var request = new TestRequest();
			testHandler.Setup(s => s.Handle(request)).Throws(new Exception("handler failed"));

			// act
			var result = Record.Exception(() => sut.Get(request));

			// assert 
			Assert.NotNull(result);
			Assert.Equal("handler failed", result.Message);
		}


		[Fact]
		public async Task Get_RegisteredAsyncHandlerThatThrowsException_ReturnsUnsuccessfulResult()
		{
			// arrange
			var autoMocker = new AutoMocker();
			var serviceProvider = autoMocker.GetMock<IServiceProvider>();
			var testHandler = new Mock<TestHandler>();
			serviceProvider.Setup(s => s.GetService(typeof(IRequestAsyncHandler<TestRequest, bool>)))
						   .Returns(testHandler.Object);
			autoMocker.Use<IEnumerable<IInterceptor>>(new List<IInterceptor>());
			var sut = autoMocker.CreateInstance<BoltOn.Mediator.Pipeline.Mediator>();
			var request = new TestRequest();
			testHandler.Setup(s => s.HandleAsync(request, default(CancellationToken))).Throws(new Exception("handler failed"));

			// act
			var result = await Record.ExceptionAsync(async () => await sut.GetAsync(request));

			// assert 
			Assert.NotNull(result);
			Assert.Equal("handler failed", result.Message);
		}

		[Fact]
		public void Get_UnregisteredHandler_ReturnsUnsuccessfulResult()
		{
			// arrange
			var autoMocker = new AutoMocker();
			var serviceProvider = autoMocker.GetMock<IServiceProvider>();
			var testHandler = new Mock<TestHandler>();
			serviceProvider.Setup(s => s.GetService(typeof(IRequestHandler<TestRequest, bool>)))
						  .Returns(null);
			autoMocker.Use<IEnumerable<IInterceptor>>(new List<IInterceptor>());
			var sut = autoMocker.CreateInstance<BoltOn.Mediator.Pipeline.Mediator>();
			var request = new TestRequest();

			// act
			var result = Record.Exception(() => sut.Get(request));

			// assert 
			Assert.NotNull(result);
			Assert.Equal(string.Format(Constants.ExceptionMessages.
									   HANDLER_NOT_FOUND, request), result.Message);
   		}

		public void Dispose()
		{
			MediatorTestHelper.LoggerStatements.Clear();
			Bootstrapper
				.Instance
				.Dispose();
		}
	}
}
