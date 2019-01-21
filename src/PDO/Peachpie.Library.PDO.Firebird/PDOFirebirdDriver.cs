﻿using Pchp.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirebirdSql.Data.FirebirdClient;
using Peachpie.Library.PDO.Utilities;

namespace Peachpie.Library.PDO.Firebird
{
    /// <summary>
    /// PDO driver for firebird
    /// </summary>
    /// <seealso cref="Peachpie.Library.PDO.PDODriver" />
    [System.Composition.Export(typeof(IPDODriver))]
    public class PDOFirebirdDriver : PDODriver
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PDOFirebirdDriver"/> class.
        /// </summary>
        public PDOFirebirdDriver() : base("firebird", FirebirdClientFactory.Instance)
        {

        }

        /// <inheritDoc />
        protected override string BuildConnectionString(ReadOnlySpan<char> dsn, string user, string password, PhpArray options)
        {
            //TODO firebird pdo parameters to dotnet connectionstring
            var csb = new FbConnectionStringBuilder(dsn.ToString());
            csb.UserID = user;
            csb.Password = password;
            return csb.ConnectionString;
        }

        /// <inheritDoc />
        public override string GetLastInsertId(PDO pdo, string name)
        {
            //TODO firebird pdo characters escaping
            using (var cmd = pdo.CreateCommand("select gen_id('" + name + "', 0) from rdb$database"))
            {
                object value = cmd.ExecuteScalar();
                return value?.ToString();
            }
        }
    }
}
