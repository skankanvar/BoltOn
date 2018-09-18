﻿using System;
using BoltOn.Logging;
using BoltOn.Utilities;

namespace BoltOn.Mediator
{
	public interface IMediator
	{
		StandardDtoReponse<TResponse> Get<TResponse>(IRequest<TResponse> request);
	}

	public class Mediator : IMediator
	{
		private readonly IoC.IServiceProvider _serviceProvider;
		private readonly IBoltOnLogger<Mediator> _logger;

		public Mediator(IoC.IServiceProvider serviceProvider, IBoltOnLogger<Mediator> logger)
		{
			_logger = logger;
			_serviceProvider = serviceProvider;
		}

		public StandardDtoReponse<TResponse> Get<TResponse>(IRequest<TResponse> request)
		{
			try
			{
				var requestType = request.GetType();
				_logger.Debug($"Resolving handler for request: {requestType}");
				var genericRequestHandlerType = typeof(IRequestHandler<,>);
				var interfaceHandlerType =
					genericRequestHandlerType.MakeGenericType(request.GetType(), typeof(TResponse));
				dynamic handler = _serviceProvider.GetInstance(interfaceHandlerType);
				Check.Requires(handler != null, string.Format(Constants.ExceptionMessages.HANDLER_NOT_FOUND, requestType));
				_logger.Debug($"Resolved handler: {handler.GetType()}");
				var response = handler.Handle(request);
				return new StandardDtoReponse<TResponse> 
				{ 
					Data = response, 
					IsSuccessful = true 
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex);
				return new StandardDtoReponse<TResponse>
				{
					IsSuccessful = false,
					Exception = ex
				};
			}
			finally
			{
			}
		}
	}
}
