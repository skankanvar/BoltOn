﻿using BoltOn.Data;
using BoltOn.Data.EF;

namespace BoltOn.Tests.Other
{
	public interface IStudentRepository : IRepository<Student>
	{
	}

	public class StudentRepository : BaseEFRepository<Student, SchoolDbContext>, IStudentRepository
    {
        public StudentRepository(IDbContextFactory dbContextFactory) : base(dbContextFactory)
        {
        }
    }
}
