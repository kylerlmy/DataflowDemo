using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataflowDemo
{
    public static class BatchBlockInsert
    {
        // The number of employees to add to the database.
        // TODO: Change this value to experiment with different numbers of 
        // employees to insert into the database.
        static readonly int insertCount = 256;

        // The size of a single batch of employees to add to the database.
        // TODO: Change this value to experiment with different batch sizes.
        static readonly int insertBatchSize = 96;

        // The source database file.
        // TODO: Change this value if Northwind.sdf is at a different location
        // on your computer.
        static readonly string sourceDatabase =
            @"C:\Program Files\Microsoft SQL Server Compact Edition\v3.5\Samples\Northwind.sdf";

        // TODO: Change this value if you require a different temporary location.
        static readonly string scratchDatabase =
            @"C:\Temp\Northwind.sdf";

    }
}
