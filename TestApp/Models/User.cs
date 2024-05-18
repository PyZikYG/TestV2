using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using TestApp.Data;

namespace TestApp.Models
{
    public class User : IdentityUser<int>
    {
        public ApplicationDbContext _context { get; set; }
        public ICollection<TestResult> TestResults { get; set; }
        public ICollection<Test> Tests { get; set; }
        public int ScoreNow { get; internal set; }

        public User() : base()
        {
            TestResults = new List<TestResult>();
            Tests = new List<Test>();
            _context = Program.context;
        }
    }
}