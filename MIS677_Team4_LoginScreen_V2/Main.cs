using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MIS677_Team4_LoginScreen_V2
{
    public partial class Main : Form
    {
        private System.Security.Cryptography.Rfc2898DeriveBytes hasher;

        public Main()
        {
            InitializeComponent();
        }
    }
}
