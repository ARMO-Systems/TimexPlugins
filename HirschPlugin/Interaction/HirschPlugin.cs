using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Data;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Windows.Forms;
using System.Xml;
using ArmoSystems.Timex.SDK.Interfaces;
using HirschPlugin;
using HirschPlugin.HirschDataSetTableAdapters;
using Timex.HirschPlugin.Properties;
using Timex.HirschPlugin.ServiceReference;

namespace Timex.HirschPlugin.Interaction
{
    [Export( typeof ( ISystemPlugin ) )]
    [ExportMetadata( "Name", "Hirsch" )]
    public sealed class HirschPlugin : ISystemPlugin
    {
        #region Constants and Fields

        private const string IntegrationKey = "Hirsch_0Plugin";
        private readonly ElementsFinder< Department > departmentFinder;
        private readonly ElementsFinder< Employee > employeeFinder;
        private readonly ElementsFinder< Post > postFinder;
        private readonly ElementsFinder< RegistrationPoint > registrationPointsFinder;
        private HirschImportControl control;
        private DEPTTableAdapter deptTableAdapter;
        private EMPTableAdapter empTableAdapter;
        private EVENTSTableAdapter eventsTableAdapter;

        private bool isImportDepartments;
        private bool isImportEmployees;
        private bool isImportPosts;
        private bool isImportRegistrationPoints;
        private READERTableAdapter readerTableAdapter;
        private SDKServiceClient sdkClient;
        private string sdkServerAddress;

        private string session;
        private string timexPassword;
        private string timexUserName;
        private H_TITLETableAdapter titleTableAdapter;

        #endregion

        #region Constructors and Destructors

        public HirschPlugin()
        {
            employeeFinder = new ElementsFinder< Employee >( item => item.IntegrationID );
            departmentFinder = new ElementsFinder< Department >( item => item.Name );
            registrationPointsFinder = new ElementsFinder< RegistrationPoint >( item => item.IntegrationID );
            postFinder = new ElementsFinder< Post >( item => item.Name );
        }

        #endregion

        private void InitReaders()
        {
            readerTableAdapter = new READERTableAdapter();
            deptTableAdapter = new DEPTTableAdapter();
            empTableAdapter = new EMPTableAdapter();
            eventsTableAdapter = new EVENTSTableAdapter();
            titleTableAdapter = new H_TITLETableAdapter();
        }

        #region ISystemPlugin

        public string GetIntegrationKey()
        {
            return IntegrationKey;
        }

        public Control GetControl()
        {
            control = new HirschImportControl();
            return control;
        }

        public void BeforeImport()
        {
            sdkClient = new SDKServiceClient( new BasicHttpBinding { MaxReceivedMessageSize = 65536 * 1024, SendTimeout = new TimeSpan( 0, 10, 0 ) }, new EndpointAddress( sdkServerAddress ) );
            isImportDepartments = isImportEmployees = isImportRegistrationPoints = false;
            InitReaders();
            session = sdkClient.LogonUser( timexUserName, timexPassword, null );
        }

        public void AfterImport()
        {
            if ( sdkClient == null )
                return;
            if ( !string.IsNullOrEmpty( session ) )
                sdkClient.LogoutUser( session );
            sdkClient = null;
        }

        public string GetSettings()
        {
            var xmlDoc = new XmlDocument();
            var rootElement = xmlDoc.CreateElement( "ROOT" );
            xmlDoc.AppendChild( rootElement );

            var connectionElement = xmlDoc.CreateElement( "HirschConnectionString" );
            connectionElement.AppendChild( xmlDoc.CreateTextNode( control.HirschConnectionString ) );
            xmlDoc.ChildNodes[ 0 ].AppendChild( connectionElement );

            var timexLoginElement = xmlDoc.CreateElement( "TimexLogin" );
            timexLoginElement.AppendChild( xmlDoc.CreateTextNode( control.TimexLogin ) );
            xmlDoc.ChildNodes[ 0 ].AppendChild( timexLoginElement );

            var timexPasswordElement = xmlDoc.CreateElement( "TimexPassword" );
            timexPasswordElement.AppendChild( xmlDoc.CreateTextNode( control.TimexPassword ) );
            xmlDoc.ChildNodes[ 0 ].AppendChild( timexPasswordElement );

            var sdkServiceAddressElement = xmlDoc.CreateElement( "SDKAddress" );
            sdkServiceAddressElement.AppendChild( xmlDoc.CreateTextNode( control.SDKAddress ) );
            xmlDoc.ChildNodes[ 0 ].AppendChild( sdkServiceAddressElement );

            return xmlDoc.OuterXml;
        }

        public void SetSettings( string settings )
        {
            if ( string.IsNullOrEmpty( settings ) )
                return;
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml( settings );
            var connString = xmlDoc.GetElementsByTagName( "HirschConnectionString" );
            Settings.Default.VelocityConnectionString = connString[ 0 ].InnerText;
            var timexLogin = xmlDoc.GetElementsByTagName( "TimexLogin" );
            timexUserName = timexLogin[ 0 ].InnerText;
            var password = xmlDoc.GetElementsByTagName( "TimexPassword" );
            timexPassword = password[ 0 ].InnerText;
            var sdkAddress = xmlDoc.GetElementsByTagName( "SDKAddress" );
            sdkServerAddress = sdkAddress[ 0 ].InnerText;
            SetSettingsToControl();
        }

        public void ImportCompanies()
        {
        }

        public void ImportDepartments()
        {
            sdkClient.HeartBeat( session );

            DataTable table = deptTableAdapter.GetData();
            departmentFinder.Reload( sdkClient.GetPackDepartments( session, 10000, 0 ) );

            var existsDepartments = new List< Department >();
            var newDepartments = new List< Department >();
            foreach ( var deptRow in table.Rows.Cast< DataRow >().Select( row => row as HirschDataSet.DEPTRow ) )
            {
                if ( string.IsNullOrEmpty( deptRow.Value ) )
                    continue;
                var department = departmentFinder.Find( deptRow.Value ) ?? new Department();
                department.IntegrationID = deptRow.Value;
                department.Name = deptRow.Value;
                if ( string.IsNullOrEmpty( department.Oid ) )
                    newDepartments.Add( department );
                else
                    existsDepartments.Add( department );
            }

            var createdDepartments = sdkClient.CreateDepartments( session, newDepartments.Count );
            for ( var i = 0; i < newDepartments.Count; i++ )
            {
                createdDepartments[ i ].Name = newDepartments[ i ].Name;
                createdDepartments[ i ].IntegrationID = newDepartments[ i ].IntegrationID;
                departmentFinder.AddIfNotExists( createdDepartments[ i ] );
            }
            if ( createdDepartments != null )
                existsDepartments.AddRange( createdDepartments );
            sdkClient.UpdateDepartments( session, existsDepartments.ToArray() );
            isImportDepartments = true;
        }

        public void ImportEmploees( bool needImportPhoto )
        {
            sdkClient.HeartBeat( session );

            employeeFinder.Reload( sdkClient.GetPackEmployees( session, 10000, 0 ) );
            if ( !isImportDepartments )
                departmentFinder.Reload( sdkClient.GetPackDepartments( session, 10000, 0 ) );
            if ( !isImportPosts )
                postFinder.Reload( sdkClient.GetPackPosts( session, 10000, 0 ) );

            var empId = -1;
            var topCount = needImportPhoto ? 1000 : 2000;
            DataTable table;
            do
            {
                var newEmployeesDetailInfo = new List< EmployeeDetailInfo >();
                var existsEmployeesDetailInfo = new List< EmployeeDetailInfo >();
                var existsEmployees = new List< Employee >();
                var newEmployees = new List< Employee >();
                table = empTableAdapter.LoadTopAfterEmpId( topCount, empId, needImportPhoto );
                var i = 0;
                foreach ( var empRow in from DataRow row in table.Rows select row as HirschDataSet.EMPRow )
                {
                    var employee = employeeFinder.Find( empRow.HostUserId.ToString( CultureInfo.InvariantCulture ) ) ?? new Employee();
                    employee.IntegrationID = empRow.HostUserId.ToString( CultureInfo.InvariantCulture );
                    employee.Name = empRow.FirstName;
                    employee.MidName = empRow.MiddleName;
                    employee.LastName = empRow.LastName;
                    employee.Post = postFinder.Find( empRow.Title.Trim() );
                    employee.Department = departmentFinder.Find( empRow.Dept.Trim() );
                    if ( string.IsNullOrEmpty( employee.Oid ) )
                    {
                        newEmployees.Add( employee );
                        if ( needImportPhoto )
                            newEmployeesDetailInfo.Add( new EmployeeDetailInfo { Photo = empRow.PHOTO } );
                    }
                    else
                    {
                        existsEmployees.Add( employee );
                        if ( needImportPhoto )
                            existsEmployeesDetailInfo.Add( new EmployeeDetailInfo { Oid = employee.Oid, Photo = empRow.PHOTO } );
                    }
                    empId = empRow.HostUserId;
                    i++;
                }
                var createdEmployees = sdkClient.CreateEmployees( session, newEmployees.Count );
                for ( i = 0; i < newEmployees.Count; i++ )
                {
                    createdEmployees[ i ].IntegrationID = newEmployees[ i ].IntegrationID;
                    createdEmployees[ i ].Name = newEmployees[ i ].Name;
                    createdEmployees[ i ].MidName = newEmployees[ i ].MidName;
                    createdEmployees[ i ].LastName = newEmployees[ i ].LastName;
                    createdEmployees[ i ].Post = newEmployees[ i ].Post;
                    createdEmployees[ i ].Department = newEmployees[ i ].Department;
                    employeeFinder.AddIfNotExists( createdEmployees[ i ] );
                    if ( needImportPhoto )
                        newEmployeesDetailInfo[ i ].Oid = createdEmployees[ i ].Oid;
                }
                if ( createdEmployees != null )
                    existsEmployees.AddRange( createdEmployees );
                sdkClient.HeartBeat( session );
                sdkClient.UpdateEmployees( session, existsEmployees.ToArray() );
                if ( needImportPhoto )
                {
                    existsEmployeesDetailInfo.AddRange( newEmployeesDetailInfo );
                    sdkClient.UpdateDetailInfoEmployees( session, existsEmployeesDetailInfo.ToArray() );
                }
                sdkClient.HeartBeat( session );
            } while ( table.Rows.Count == topCount );

            isImportEmployees = true;
        }

        public void ImportPosts()
        {
            sdkClient.HeartBeat( session );

            DataTable table = titleTableAdapter.GetData();
            postFinder.Reload( sdkClient.GetPackPosts( session, 10000, 0 ) );

            var existsPosts = new List< Post >();
            var newPosts = new List< Post >();
            foreach ( var titleRow in from DataRow row in table.Rows select row as HirschDataSet.H_TITLERow )
            {
                if ( string.IsNullOrEmpty( titleRow.Value ) )
                    continue;
                var post = postFinder.Find( titleRow.Value ) ?? new Post();
                post.Name = titleRow.Value;
                post.IntegrationID = titleRow.Value;
                if ( string.IsNullOrEmpty( post.Oid ) )
                    newPosts.Add( post );
                else
                    existsPosts.Add( post );
            }
            var createdPosts = sdkClient.CreatePosts( session, newPosts.Count );
            for ( var i = 0; i < newPosts.Count; i++ )
            {
                createdPosts[ i ].Name = newPosts[ i ].Name;
                createdPosts[ i ].IntegrationID = newPosts[ i ].IntegrationID;
                postFinder.AddIfNotExists( createdPosts[ i ] );
            }
            if ( createdPosts != null )
                existsPosts.AddRange( createdPosts );
            sdkClient.UpdatePosts( session, existsPosts.ToArray() );
            isImportPosts = true;
        }

        public void ImportRegistrationPoints()
        {
            sdkClient.HeartBeat( session );

            DataTable table = readerTableAdapter.GetData();
            registrationPointsFinder.Reload( sdkClient.GetPackRegistrationPoints( session, 10000, 0 ) );

            var existsRegistrationPoints = new List< RegistrationPoint >();
            var newRegistrationPoints = new List< RegistrationPoint >();
            foreach ( var doorRow in from DataRow row in table.Rows select row as HirschDataSet.READERRow )
            {
                var registrationPoint = registrationPointsFinder.Find( doorRow.Address ) ?? new RegistrationPoint();
                registrationPoint.Name = doorRow.Name;
                registrationPoint.IntegrationID = doorRow.Address;
                if ( string.IsNullOrEmpty( registrationPoint.Oid ) )
                    newRegistrationPoints.Add( registrationPoint );
                else
                    existsRegistrationPoints.Add( registrationPoint );
            }
            var createdRegistrationPoints = sdkClient.CreateRegistrationPoints( session, newRegistrationPoints.Count );
            for ( var i = 0; i < newRegistrationPoints.Count; i++ )
            {
                createdRegistrationPoints[ i ].Name = newRegistrationPoints[ i ].Name;
                createdRegistrationPoints[ i ].IntegrationID = newRegistrationPoints[ i ].IntegrationID;
                registrationPointsFinder.AddIfNotExists( createdRegistrationPoints[ i ] );
            }
            if ( createdRegistrationPoints != null )
                existsRegistrationPoints.AddRange( createdRegistrationPoints );
            sdkClient.UpdateRegistrationPoints( session, existsRegistrationPoints.ToArray() );
            isImportRegistrationPoints = true;
        }

        public void ImportEvents()
        {
            sdkClient.HeartBeat( session );
            if ( !isImportEmployees )
                employeeFinder.Reload( sdkClient.GetPackEmployees( session, 10000, 0 ) );
            if ( !isImportRegistrationPoints )
                registrationPointsFinder.Reload( sdkClient.GetPackRegistrationPoints( session, 10000, 0 ) );
            if ( employeeFinder.IsEmpty || registrationPointsFinder.IsEmpty )
                return;

            var startDate = new DateTime( 1900, 1, 1 );
            var timeZoneOid = sdkClient.GetPackTimeZones( session, 10000, 0 ).FirstOrDefault( timeZone => string.Equals( timeZone.ID, TimeZoneInfo.Local.Id, StringComparison.InvariantCulture ) ).Oid;
            var lastEvent = sdkClient.GetWorktimeEvents( session, startDate.ToUniversalTime(), DateTime.Today.ToUniversalTime(), timeZoneOid, null, 1, true, IntegrationKey ).FirstOrDefault();
            DataTable table = eventsTableAdapter.GetDataByNextTime__StartTime_( ( lastEvent == null ? startDate : lastEvent.TimeUTC ).AddDays( -7 ) );

            foreach ( var eventRow in from DataRow row in table.Rows select row as HirschDataSet.EVENTSRow )
            {
                sdkClient.CreateWorktimeEvent( session, IntegrationKey, employeeFinder.Find( eventRow.HostUserId.ToString( CultureInfo.InvariantCulture ) ).Oid,
                    registrationPointsFinder.Find( eventRow.NetAddress.ToString( CultureInfo.InvariantCulture ) ).Oid, eventRow.dtDate, timeZoneOid );
            }
        }

        #endregion

        #region Methods

        private void SetSettingsToControl()
        {
            if ( control == null )
                return;
            control.HirschConnectionString = Settings.Default.VelocityConnectionString;
            control.TimexLogin = timexUserName;
            control.TimexPassword = timexPassword;
            control.SDKAddress = sdkServerAddress;
        }

        #endregion
    }
}