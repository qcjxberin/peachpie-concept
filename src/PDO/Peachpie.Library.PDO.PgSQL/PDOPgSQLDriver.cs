﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Pchp.Core;

namespace Peachpie.Library.PDO.PgSQL
{
    /// <summary>
    /// PDO driver class for postgresql
    /// </summary>
    /// <seealso cref="Peachpie.Library.PDO.PDODriver" />
    [System.Composition.Export(typeof(IPDODriver))]
    public class PDONpgsqlDriver : PDODriver
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PDONpgsqlDriver"/> class.
        /// </summary>
        public PDONpgsqlDriver() : base("pgsql", NpgsqlFactory.Instance)
        {

        }

        /// <inheritDoc />
        protected override string BuildConnectionString(ReadOnlySpan<char> dsn, string user, string password, PhpArray options)
        {
            //TODO pgsql pdo parameters to dotnet connectionstring
            var csb = new NpgsqlConnectionStringBuilder(dsn.ToString());
            csb.Username = user;
            csb.Password = password;
            return csb.ConnectionString;
        }

        /// <inheritDoc />
        public override string GetLastInsertId(PDO pdo, string name)
        {
            throw new NotImplementedException();
        }
    }
}
