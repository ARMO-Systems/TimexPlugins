using System.Data;
using System.Globalization;

namespace HirschPlugin.HirschDataSetTableAdapters
{
    partial class EMPTableAdapter
    {
        #region Public Methods and Operators

        public DataTable LoadTopAfterEmpId( int topCount, int empId, bool loadPhoto )
        {
            var previosCommand1 = CommandCollection[ 0 ].CommandText;
            var previosCommand2 = CommandCollection[ 1 ].CommandText;
            try
            {
                CommandCollection[ 0 ].CommandText = CommandCollection[ 0 ].CommandText.Replace( "1000", topCount.ToString( CultureInfo.InvariantCulture ) );
                CommandCollection[ 1 ].CommandText = CommandCollection[ 1 ].CommandText.Replace( "1000", topCount.ToString( CultureInfo.InvariantCulture ) );
                return !loadPhoto ? GetTopData( empId ) : GetTopDataPhoto( empId );
            }
            finally
            {
                CommandCollection[ 0 ].CommandText = previosCommand1;
                CommandCollection[ 1 ].CommandText = previosCommand2;
            }
        }

        #endregion
    }
}

namespace HirschPlugin
{
    partial class HirschDataSet
    {
        #region Nested type: DEPTDataTable

        partial class DEPTDataTable
        {
        }

        #endregion
    }
}