﻿// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.UI.WebControls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using Rock;
using Rock.Cache;
using Rock.Data;
using Rock.Model;

namespace Rock.Utility.NcoaApi
{
    /// <summary>
    /// TrueNCOA API calls
    /// </summary>
    public class TrueNcoaApi
    {
        private string trueNcoaServer = "https://app.testing.truencoa.com/api/"; // "https://app.truencoa.com/api/";
        private string TRUE_NCOA_SERVER = "https://app.testing.truencoa.com";
        private int batchsize = 100;
        private string username = "gerhard@sparkdevnetwork.org";
        private string password = "testTrueNCOA";
        private RestClient client = null;
        private string _id;


        /// <summary>
        /// Initializes a new instance of the <see cref="TrueNcoaApi"/> class.
        /// </summary>
        /// <param name="id">The identifier.</param>
        public TrueNcoaApi( string id )
        {
            CreateRestClient();
            _id = id;
        }

        /// <summary>
        /// Creates the rest client.
        /// </summary>
        private void CreateRestClient()
        {
            client = new RestClient( TRUE_NCOA_SERVER );
            client.AddDefaultHeader( "user_name", username );
            client.AddDefaultHeader( "password", password );
            client.AddDefaultHeader( "content-type", "application/x-www-form-urlencoded" );
        }


        public bool UploadAddresses( Dictionary<int, PersonAddressItem> addresses, string fileName )
        {
            try
            {
                PersonAddressItem[] addressArray = addresses.Values.ToArray();
                StringBuilder data = new StringBuilder();
                for ( int i = 1; i <= addressArray.Length; i++ )
                {
                    PersonAddressItem personAddressItem = addressArray[i - 1];
                    data.AppendFormat( "{0}={1}&", "individual_id", $"{personAddressItem.PersonId}_{personAddressItem.PersonAliasId}_{personAddressItem.FamilyId}" );
                    data.AppendFormat( "{0}={1}&", "individual_first_name", personAddressItem.FirstName );
                    data.AppendFormat( "{0}={1}&", "individual_last_name", personAddressItem.LastName );
                    data.AppendFormat( "{0}={1}&", "address_line_1", personAddressItem.Street1 );
                    data.AppendFormat( "{0}={1}&", "address_line_2", personAddressItem.Street2 );
                    data.AppendFormat( "{0}={1}&", "address_city_name", personAddressItem.City );
                    data.AppendFormat( "{0}={1}&", "address_state_code", personAddressItem.State );
                    data.AppendFormat( "{0}={1}&", "address_postal_code", personAddressItem.PostalCode );
                    data.AppendFormat( "{0}={1}&", "address_country_code", personAddressItem.Country );

                    if ( i % batchsize == 0 || i == addressArray.Length )
                    {
                        var request = new RestRequest( $"api/files/{fileName}/records", Method.POST );
                        request.AddHeader( "id", _id );
                        request.AddParameter( "application/x-www-form-urlencoded", data.ToString().TrimEnd( '&' ), ParameterType.RequestBody );
                        IRestResponse response = client.Execute( request );
                        if ( response.StatusCode != HttpStatusCode.OK )
                        {
                            //Todo: message
                            return false;
                        }

                        data = new StringBuilder();
                    }
                }
            }
            catch ( Exception ex )
            {
                return false;
            }

            try
            {
                var request = new RestRequest( $"api/files/{fileName}", Method.GET );
                request.AddHeader( "id", _id );
                request.AddParameter( "application/x-www-form-urlencoded", "status=submit", ParameterType.RequestBody );
                IRestResponse response = client.Execute( request );
                if ( response.StatusCode != HttpStatusCode.OK )
                {
                    //Todo: message
                    return false;
                }

                try
                {
                    TrueNcoaResponse file = JsonConvert.DeserializeObject<TrueNcoaResponse>( response.Content );
                    if ( file.Status != "Mapped" )
                    {
                        Console.WriteLine( $"The filename: {fileName} is not in the correct status" );
                        return false;
                    }
                }
                catch ( Exception ex )
                {
                    Console.WriteLine( $"Invalid response: {response.Content}" ); //todo: update exception
                    return false;
                }
            }
            catch ( Exception ex )
            {
                return false;
            }

            return true;
        }

        public bool CreateReport( string fileName )
        {
            try
            {
                // submit for processing
                var request = new RestRequest( $"api/files/{fileName}", Method.PATCH );
                request.AddHeader( "id", _id );
                request.AddParameter( "application/x-www-form-urlencoded", "status=submit", ParameterType.RequestBody );
                IRestResponse response = client.Execute( request );
                if ( response.StatusCode != HttpStatusCode.OK )
                {
                    //Todo: message
                    return false;
                }

                return true;
            }
            catch ( Exception ex )
            {
                return false;
            }
        }

        public bool IsReportCreated( string fileName )
        {
            try
            {
                var request = new RestRequest( $"api/files/{fileName}", Method.GET );
                request.AddHeader( "id", _id );
                IRestResponse response = client.Execute( request );
                if ( response.StatusCode != HttpStatusCode.OK )
                {
                    //Todo: message
                    return false;
                }

                try
                {
                    TrueNcoaResponse file = JsonConvert.DeserializeObject<TrueNcoaResponse>( response.Content );
                    bool processing = ( file.Status == "Import" || file.Status == "Importing" || file.Status == "Parse" || file.Status == "Parsing" || file.Status == "Report" || file.Status == "Reporting" || file.Status == "Process" || file.Status == "Processing" );
                    return !processing;
                }
                catch ( Exception ex )
                {
                    Console.WriteLine( $"Invalid response: {response.Content}" ); //todo: update exception
                    return false;
                }
            }
            catch ( Exception ex )
            {
                return false;
            }
        }

        public bool CreateReportExport( string fileName, out string exportfileid )
        {
            exportfileid = null;
            try
            {
                // submit for exporting
                var request = new RestRequest( $"api/files/{fileName}", Method.PATCH );
                request.AddHeader( "id", _id );
                request.AddParameter( "application/x-www-form-urlencoded", "status=export", ParameterType.RequestBody );
                IRestResponse response = client.Execute( request );
                if ( response.StatusCode != HttpStatusCode.OK )
                {
                    //Todo: message
                    return false;
                }

                try
                {
                    TrueNcoaResponse file = JsonConvert.DeserializeObject<TrueNcoaResponse>( response.Content );
                    exportfileid = file.Id;
                }
                catch ( Exception ex )
                {
                    Console.WriteLine( $"Invalid response: {response.Content}" ); //todo: update exception
                    return false;
                }

                return true;
            }
            catch ( Exception ex )
            {
                return false;
            }

        }

        public bool IsReportExportCreated( string exportfileid )
        {
            try
            {
                var request = new RestRequest( $"api/files/{exportfileid}", Method.GET );
                request.AddHeader( "id", _id );
                IRestResponse response = client.Execute( request );
                if ( response.StatusCode != HttpStatusCode.OK )
                {
                    //Todo: message
                    return false;
                }

                try
                {
                    TrueNcoaResponse file = JsonConvert.DeserializeObject<TrueNcoaResponse>( response.Content );
                    bool exporting = ( file.Status == "Export" || file.Status == "Exporting" );
                    return !exporting;
                }
                catch ( Exception ex )
                {
                    Console.WriteLine( $"Invalid response: {response.Content}" ); //todo: update exception
                    return false;
                }
            }
            catch ( Exception ex )
            {
                return false;
            }
        }

        public bool DownloadExport( string exportfileid, out List<TrueNcoaReturnRecord> records )
        {
            records = null;

            try
            {
                var request = new RestRequest( $"api/files/{exportfileid}/records", Method.GET );
                request.AddHeader( "id", _id );
                request.AddParameter( "application/x-www-form-urlencoded", "status=submit", ParameterType.RequestBody );
                IRestResponse response = client.Execute( request );
                if ( response.StatusCode != HttpStatusCode.OK )
                {
                    //Todo: message
                    return false;
                }

                try
                {
                    TrueNcoaResponse file = JsonConvert.DeserializeObject<TrueNcoaResponse>( response.Content );
                    var obj = JObject.Parse( response.Content );
                    var recordsjson = (string)obj["Records"].ToString();
                    records = JsonConvert.DeserializeObject<List<TrueNcoaReturnRecord>>( recordsjson );
                    DateTime dt = DateTime.Now;
                    records.ForEach( r => r.NcoaRunDateTime = dt );
                }
                catch ( Exception ex )
                {
                    Console.WriteLine( $"Invalid response: {response.Content}" ); //todo: update exception
                    return false;
                }


                return true;
            }
            catch ( Exception ex )
            {
                return false;
            }
        }

        public void SaveRecords( List<TrueNcoaReturnRecord> records, string fileName )
        {
            DataTable dtRecords = null;
            string recordsjson = JsonConvert.SerializeObject( records );
            dtRecords = (DataTable)JsonConvert.DeserializeObject( recordsjson, ( typeof( DataTable ) ) );
            StringBuilder sb = new StringBuilder();
            IEnumerable<string> columnNames = dtRecords.Columns.Cast<DataColumn>().Select( column => column.ColumnName );
            sb.AppendLine( string.Join( ",", columnNames ) );
            foreach ( DataRow row in dtRecords.Rows )
            {
                IEnumerable<string> fields = row.ItemArray.Select( field => string.Concat( "\"", field.ToString().Replace( "\"", "\"\"" ), "\"" ) );
                sb.AppendLine( string.Join( ",", fields ) );
            }

            if ( System.IO.File.Exists( fileName ) )
            {
                System.IO.File.Delete( fileName );
            }

            System.IO.File.WriteAllText( fileName, sb.ToString() );
        }
    }
}
