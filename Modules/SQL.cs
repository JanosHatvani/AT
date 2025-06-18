using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Modules
{

   public class SQL
    {
        public static void CreateDB()
        {
            string connectionString = "Data Source=localhost;Integrated Security=True;Initial Catalog=;";
            string commandText = "create database ATDB on my primary(name=atdb,filename='C:\\USERS\\atDB.mdf',size=10,maxsize=20MB,filegrowth=10%)" +
              " LOG ON(name=atdb_log,filename='C:\\USERS\\atDB.ldf',size=5,maxsize=10MB,filegrowth=1%)";
            SqlConnection sqlConnection = new SqlConnection(connectionString);
            if (sqlConnection.State != System.Data.ConnectionState.Open);
            {
                try
                {
                    sqlConnection.Open();
                    MessageBox.Show("Az adatbázis létrehozva", "MyProgram", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SqlCommand sqlCommand = new SqlCommand(commandText, sqlConnection);
                    sqlCommand.ExecuteNonQuery();
                    sqlConnection.Close();
                    connectionString = "Data Source=localhost;Integrated Security=True;Initial Catalog=ATDB;";
                    sqlConnection = new SqlConnection(connectionString);
                    sqlConnection.Open();
                    commandText = "Create table comparet(id int not null primary key identity(1,1),"+
                        "cikkszam varchar(250)," +
                        "priceA varchar(50)," +
                        "priceB varchar(50)," +
                        "date date not null," +
                        "compared varchar(1)";
                    sqlCommand = sqlConnection.CreateCommand();
                    sqlCommand.CommandText = commandText;
                    sqlCommand.ExecuteNonQuery();                    
                    MessageBox.Show("Az adatbázis létrehozva", "MyProgram", MessageBoxButtons.OK, MessageBoxIcon.Information);

                }
                catch(SqlException e)
                {
                    MessageBox.Show(e.ToString(), "MyProgram", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                finally
                {
                    sqlConnection.Close();
                    MessageBox.Show("A kapcsoalt bezárva", "MyProgram", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        //public void JoinDB()
        //{
        //    string connectionString = "Data Source=(LocalDB)\MSSQLLocalDB;Integrated Security=True;Initial Catalog=;";
        //    using (SqlConnection connection = new SqlConnection(connectionString))
        //    {
        //        connection.Open();
        //    }
        //}
    }

 
}
