using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataflowDemo
{
    // Describes an employee. Each property maps to a 
    // column in the Employees table in the Northwind database.
    // For brevity, the Employee class does not contain
    // all columns from the Employees table.
    class Employee
    {
        public int EmployeeID { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }

        // A random number generator that helps tp generate
        // Employee property values.
        static Random rand = new Random(42);

        // Possible random first names.
        static readonly string[] firstNames = { "Tom", "Mike", "Ruth", "Bob", "John" };
        // Possible random last names.
        static readonly string[] lastNames = { "Jones", "Smith", "Johnson", "Walker" };

        // Creates an Employee object that contains random 
        // property values.
        public static Employee Random()
        {
            return new Employee
            {
                EmployeeID = -1,
                LastName = lastNames[rand.Next() % lastNames.Length],
                FirstName = firstNames[rand.Next() % firstNames.Length]
            };
        }
    }
}
