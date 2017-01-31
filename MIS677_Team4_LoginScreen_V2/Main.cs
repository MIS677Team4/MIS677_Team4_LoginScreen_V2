using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Data.SqlClient;
using System.Configuration;

namespace MIS677_Team4_LoginScreen_V2
{
    public partial class Main : Form
    {
        private Login log;
        private dbManager db;
        public Main()
        {
            InitializeComponent();
            log = null;
            db = new MIS677_Team4_LoginScreen_V2.dbManager();
        }

        /// <summary>
        /// Enables access if the user is logged in when the button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void accessButton_Click(object sender, EventArgs e)
        {
            if (log == null || !log.isLogged)
                MessageBox.Show("Unable to access - please log in!");
            else if (log.isLogged)
                MessageBox.Show("You have access!");
        }

        /// <summary>
        /// Tries to log the user in given the username and password in the text fields
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void loginLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            log = new MIS677_Team4_LoginScreen_V2.Login(unameTB.Text, passTB.Text);
            if (log.attemptLogin(db))
            {
                loginLink.Enabled = false;
                logoutLink.Enabled = true;
                unameTB.Text = "";
                passTB.Text = "";
            }
            else
            {
                log = null;
                passTB.Text = "";
            }
        }

        /// <summary>
        /// If the user is logged in, logs them out
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void logoutLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            log = null;
            loginLink.Enabled = true;
            logoutLink.Enabled = false;
        }
    }

    class Hasher
    {
        private const int saltlength = 16;
        private const int hashlength = 32;
        private const int iterations = 12000;
        SoapHexBinary shb;

        /// <summary>
        /// Hashes the entered password and compares to the pulled password hash
        /// </summary>
        /// <param name="p"></param>
        /// <param name="s"></param>
        /// <param name="h"></param>
        /// <returns></returns>
        public Boolean Comapre(string p, string s, string h)
        {
            bool match = true;
            byte[] salt = new byte[saltlength];
            byte[] checkhash = new byte[hashlength];
            byte[] passhash = new byte[hashlength];
            salt = getStringToBytes(s);
            checkhash = getStringToBytes(h);
            passhash = hash(p, salt, hashlength, iterations);
            for (int x = 0; x < hashlength; x++)
                if (checkhash[x] != passhash[x])
                    match = false;
            return match;
        }

        /// <summary>
        /// Function from user Mycroft on StackOverflow
        /// Converts a hex string to a bye array
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private byte[] getStringToBytes(string value)
        {
            shb = SoapHexBinary.Parse(value);
            return shb.Value;
        }

        /// <summary>
        /// Hashes the provided password using the provided salt
        /// </summary>
        /// <param name="p"></param>
        /// <param name="s"></param>
        /// <param name="outputBytes"></param>
        /// <param name="iter"></param>
        /// <returns></returns>
        private static byte[] hash(string p, byte[] s, int outputBytes, int iter)
        {
            System.Security.Cryptography.Rfc2898DeriveBytes hasher = new System.Security.Cryptography.Rfc2898DeriveBytes(p, s, iter);
            return hasher.GetBytes(outputBytes);
        }
    }

    class Login
    {
        private bool logged;
        private string uname;
        private string pass;
        private Hasher hs;

        private System.Text.RegularExpressions.Regex parser;
        public bool isLogged { get { return logged; } }
        public string user { get { return uname; } }

        /// <summary>
        /// Creates the temporary user record
        /// </summary>
        /// <param name="u"></param>
        /// <param name="p"></param>
        public Login(string u, string p)
        {
            uname = u;
            pass = p;
            parser = new System.Text.RegularExpressions.Regex("[^A-Za-z0-9_]");
            hs = new MIS677_Team4_LoginScreen_V2.Hasher();
        }

        /// <summary>
        /// Attempts to log the user in, using the dbManager class to conduct operations against the database
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public bool attemptLogin(dbManager db)
        {
            DataRow pullrow;
            DataRow lockrow;

            if (parser.Match(uname).Success || parser.Match(pass).Success)
            {
                MessageBox.Show("Username and Password must only contain alphanumerics and underscores!");
                return false;
            }
            else
            {
                lockrow = db.checkLock(uname);
                if (lockrow != null)
                {
                    if (!bool.Parse(lockrow[0].ToString()))
                    {
                        pullrow = db.getLogin(uname);
                        if (pullrow != null)
                        {
                            if (hs.Comapre(pass, pullrow[0].ToString(), pullrow[1].ToString()))
                            {
                                logged = true;
                                MessageBox.Show("Successfully Logged In!");
                                db.logAttempt(uname, 1);
                                return true;
                            }
                            else
                            {
                                db.logAttempt(uname, 0);
                                db.increaseLockout(uname);
                                MessageBox.Show("Unable to log in - incorrect password.");
                                return false;
                            }
                        }
                        else
                        {
                            MessageBox.Show("Unable to log in - database error!");
                            return false;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Unable to log in - " + uname + " is locked out! Please contact your administrator!");
                        db.logAttempt(uname, 0);
                        return false;
                    }
                }
                else
                {
                    MessageBox.Show("Unable to log in - incorrect username!");
                    return false;
                }
            }
        }

    }

    class dbManager
    {
        SqlCommand cmd;
        SqlConnection connect;
        SqlDataAdapter da;

        public dbManager()
        {
            connect = new SqlConnection(ConfigurationManager.ConnectionStrings["MIS677_Team4_LoginScreen_V2.Properties.Settings.GROUP4ConnectionString"].ConnectionString);
        }

        /// <summary>
        /// Tries to get the salt and password hash from the database. If unable to, returns a null.
        /// </summary>
        /// <param name="u"></param>
        /// <returns></returns>
        public DataRow getLogin(string u)
        {
            DataSet ds = new DataSet();
            DataTable dt;

            if (connect != null)
            {
                try
                {
                    if (connect.State == ConnectionState.Closed) { connect.Open(); }

                    cmd = new SqlCommand();
                    cmd.Connection = connect;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "Hashpull";
                    cmd.Parameters.AddWithValue("@Uname", u);
                    da = new SqlDataAdapter(cmd);
                    da.Fill(ds);
                    dt = (ds.Tables.Count > 0 ? ds.Tables[0] : null);
                    return (dt != null && dt.Rows.Count > 0 ? dt.Rows[0] : null);
                }

                catch (Exception se) { MessageBox.Show("Error" + se.ToString()); }

                finally
                {
                    connect.Close();
                    cmd.Dispose();
                }
            }

            return null;
        }

        /// <summary>
        /// Checks to see if the username is locked out.
        /// </summary>
        /// <param name="u"></param>
        /// <returns></returns>
        public DataRow checkLock(string u)
        {
            DataSet ds = new DataSet();
            DataTable dt;

            if (connect != null)
            {
                try
                {
                    if (connect.State == ConnectionState.Closed) { connect.Open(); }

                    cmd = new SqlCommand();
                    cmd.Connection = connect;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "CheckLockout";
                    cmd.Parameters.AddWithValue("@Uname", u);
                    da = new SqlDataAdapter(cmd);
                    da.Fill(ds);
                    dt = (ds.Tables.Count > 0 ? ds.Tables[0] : null);
                    return (dt != null && dt.Rows.Count > 0 ? dt.Rows[0] : null);
                }

                catch (Exception se) { MessageBox.Show("Error" + se.ToString()); }

                finally
                {
                    connect.Close();
                    cmd.Dispose();
                }
            }

            return null;
        }

        /// <summary>
        /// Log a login attept with the given username.
        /// </summary>
        /// <param name="u"></param>
        /// <param name="b"></param>
        public void logAttempt(string u, int b)
        {
            if (connect != null)
            {
                try
                {
                    if (connect.State == ConnectionState.Closed) { connect.Open(); }

                    cmd = new SqlCommand();
                    cmd.Connection = connect;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "RecordAttempt";
                    cmd.Parameters.AddWithValue("@Uname", u);
                    cmd.Parameters.AddWithValue("@Success", b);
                    cmd.ExecuteNonQuery();
                }

                catch (Exception se) { MessageBox.Show("Error" + se.ToString()); }

                finally
                {
                    connect.Close();
                    cmd.Dispose();
                }
            }
        }


        /// <summary>
        /// Increments the attempt for that username until the lockout is met.
        /// </summary>
        /// <param name="u"></param>
        public void increaseLockout(string u)
        {
            if (connect != null)
            {
                try
                {
                    if (connect.State == ConnectionState.Closed) { connect.Open(); }

                    cmd = new SqlCommand();
                    cmd.Connection = connect;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "Lockout";
                    cmd.Parameters.AddWithValue("@Uname", u);
                    cmd.ExecuteNonQuery();
                }

                catch (Exception se) { MessageBox.Show("Error" + se.ToString()); }

                finally
                {
                    connect.Close();
                    cmd.Dispose();
                }
            }
        }
    }
}
