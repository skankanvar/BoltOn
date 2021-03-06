﻿using BoltOn.Mediator.Interceptors;

namespace BoltOn.Mediator.Pipeline
{
	public interface IRequest<out TResponse>
	{
	}

	public interface IRequest : IRequest<DummyResponse>
	{
	}

	public interface ICommand : IRequest, IEnableUnitOfWorkInterceptor
	{
	}

	public interface ICommand<out TResponse> : IRequest<TResponse>, IEnableUnitOfWorkInterceptor
	{
	}

	public interface IQuery<out TResponse> : IRequest<TResponse>, IEnableUnitOfWorkInterceptor
	{
	}

	public interface IStaleQuery<out TResponse> : IRequest<TResponse>, IEnableUnitOfWorkInterceptor
	{
	}
	
	public class DummyResponse
	{
	}
}
