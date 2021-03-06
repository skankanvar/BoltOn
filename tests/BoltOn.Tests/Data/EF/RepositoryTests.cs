using System;
using BoltOn.Data.EF;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BoltOn.Bootstrapping;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using BoltOn.Tests.Other;

namespace BoltOn.Tests.Data.EF
{
	// Collection is used to prevent running tests in parallel i.e., tests in the same collection
	// will not be executed in parallel
	[Collection("IntegrationTests")]
	public class RepositoryTests : IDisposable
	{
		private IStudentRepository _sut;

		public RepositoryTests()
		{
			// this flag can be set to true for [few] tests. Running all the tests with this set to true might slow down.
			MediatorTestHelper.IsSqlServer = false;
			var serviceCollection = new ServiceCollection();
			serviceCollection
				.BoltOn(options =>
				{
					options
						.BoltOnEFModule();
				});
			var serviceProvider = serviceCollection.BuildServiceProvider();
			serviceProvider.TightenBolts();
			MediatorTestHelper.IsSeedData = true;
			_sut = serviceProvider.GetService<IStudentRepository>();
		}

		[Fact, Trait("Category", "Integration")]
		public void GetById_WhenRecordExists_ReturnsRecord()
		{
			// arrange

			// act
			var result = _sut.GetById(1);

			// assert
			Assert.NotNull(result);
			Assert.Equal("a", result.FirstName);
		}

		[Fact, Trait("Category", "Integration")]
		public void GetById_WhenRecordDoesNotExist_ReturnsNull()
		{
			// arrange

			// act
			var result = _sut.GetById(3);

			// assert
			Assert.Null(result);
		}

		[Fact, Trait("Category", "Integration")]
		public async Task GetByIdAsync_WhenRecordExists_ReturnsRecord()
		{
			// arrange

			// act
			var result = await _sut.GetByIdAsync(1);

			// assert
			Assert.NotNull(result);
			Assert.Equal("a", result.FirstName);
		}

		[Fact, Trait("Category", "Integration")]
		public void GetAll_WhenRecordsExist_ReturnsAllTheRecords()
		{
			// arrange

			// act
			var result = _sut.GetAll().ToList();

			// assert
			Assert.Equal(2, result.Count);
		}

		[Fact, Trait("Category", "Integration")]
		public async Task GetAllAsync_WhenRecordsExist_ReturnsAllTheRecords()
		{
			// arrange

			// act
			var result = await _sut.GetAllAsync();

			// assert
			Assert.Equal(2, result.Count());
		}

		[Fact, Trait("Category", "Integration")]
		public void FindByWithoutIncludes_WhenRecordsExist_ReturnsRecordsThatMatchesTheFindByCriteria()
		{
			// arrange

			// act
			var result = _sut.FindBy(f => f.Id == 2).FirstOrDefault();

			// assert
			Assert.NotNull(result);
			Assert.Equal("x", result.FirstName);
		}

		[Fact, Trait("Category", "Integration")]
		public void FindByWithIncludes_WhenRecordsExist_ReturnsRecordsThatMatchesTheCriteria()
		{
			// arrange

			// act
			var result = _sut.FindBy(f => f.Id == 2, f => f.Addresses).FirstOrDefault();

			// assert
			Assert.NotNull(result);
			Assert.Equal("x", result.FirstName);
			Assert.NotEmpty(result.Addresses);
		}

		[Fact, Trait("Category", "Integration")]
		public async Task FindByAsyncWithIncludes_WhenRecordsExist_ReturnsRecordsThatMatchesTheCriteria()
		{
			// arrange

			// act
			var result = (await _sut.FindByAsync(f => f.Id == 2, default(CancellationToken), f => f.Addresses)).FirstOrDefault();

			// assert
			Assert.NotNull(result);
			Assert.Equal("x", result.FirstName);
			Assert.NotEmpty(result.Addresses);
		}

		[Fact, Trait("Category", "Integration")]
		public void Add_AddANewEntity_ReturnsAddedEntity()
		{
			// arrange
			const int newStudentId = 5;
			var student = new Student
			{
				Id = newStudentId,
				FirstName = "c",
				LastName = "d"
			};

			// act
			var result = _sut.Add(student);
			var queryResult = _sut.GetById(newStudentId);

			// assert
			Assert.NotNull(queryResult);
			Assert.Equal("c", queryResult.FirstName);
			Assert.Equal(result.FirstName, queryResult.FirstName);
		}

		[Fact, Trait("Category", "Integration")]
		public async Task AddAsync_AddANewEntity_ReturnsAddedEntity()
		{
			// arrange
			const int newStudentId = 6;
			var student = new Student
			{
				Id = newStudentId,
				FirstName = "c",
				LastName = "d"
			};

			// act
			var result = await _sut.AddAsync(student);
			var queryResult = _sut.GetById(newStudentId);

			// assert
			Assert.NotNull(queryResult);
			Assert.Equal("c", queryResult.FirstName);
			Assert.Equal(result.FirstName, queryResult.FirstName);
		}

		[Fact, Trait("Category", "Integration")]
		public void Update_UpdateAnExistingEntity_UpdatesTheEntity()
		{
			// arrange
			var student = _sut.GetById(2);

			// act
			student.FirstName = "c";
			_sut.Update(student);
			var queryResult = _sut.GetById(2);

			// assert
			Assert.NotNull(queryResult);
			Assert.Equal("c", queryResult.FirstName);
		}

		[Fact, Trait("Category", "Integration")]
		public async Task UpdateAsync_UpdateAnExistingEntity_UpdatesTheEntity()
		{
			// arrange
			var student = _sut.GetById(2);

			// act
			student.FirstName = "c";
			await _sut.UpdateAsync(student);
			var queryResult = _sut.GetById(2);

			// assert
			Assert.NotNull(queryResult);
			Assert.Equal("c", queryResult.FirstName);
		}

		public void Dispose()
		{
			Bootstrapper
				.Instance
				.Dispose();
		}
	}
}
