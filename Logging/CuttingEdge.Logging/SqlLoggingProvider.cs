﻿#region Copyright (c) 2008 S. van Deursen
/* The CuttingEdge.Logging library allows developers to plug a logging mechanism into their web- and desktop
 * applications.
 * 
 * Copyright (C) 2008 S. van Deursen
 * 
 * To contact me, please visit my blog at http://www.cuttingedge.it/blogs/steven/ or mail to steven at 
 * cuttingedge.it.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
 * associated documentation files (the "Software"), to deal in the Software without restriction, including 
 * without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
 * copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the 
 * following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or substantial 
 * portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
 * LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO 
 * EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER 
 * IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE 
 * USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion

using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Configuration.Provider;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace CuttingEdge.Logging
{
    /// <summary>
    /// Manages storage of logging information in a SQL Server database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used by the <see cref="Logger"/> class to provide Logging services for an 
    /// application using a SQL Server database. You cannot use a <see cref="SqlLoggingProvider"/>
    /// without SQL Server.
    /// </para>
    /// <para>
    /// The table below shows the list of valid attributes for the <see cref="SqlLoggingProvider"/>:
    /// <list type="table">  
    /// <listheader>
    ///     <attribute>Attribute</attribute>
    ///     <description>Description</description>
    /// </listheader>
    /// <item>
    ///     <attribute>fallbackProvider</attribute>
    ///     <description>
    ///         A fallback provider that the Logger class will use when logging failed on this logging 
    ///         provider. The value must contain the name of an existing logging provider. This attribute is
    ///         optional.
    ///     </description>
    /// </item>  
    /// <item>
    ///     <attribute>threshold</attribute>
    ///     <description>
    ///         The logging threshold. The threshold limits the number of event logged. The threshold can be
    ///         defined as follows: Debug &lt; Information &lt; Warning &lt; Error &lt; Fatal. i.e., When the 
    ///         threshold is set to Information, Debug events will not be logged. When no value is specified
    ///         all events are logged. This attribute is optional.
    ///      </description>
    /// </item>  
    /// <item>
    ///     <attribute>connectionStringName</attribute>
    ///     <description>
    ///         The connection string provided with this provider. This attribute is mandatory.
    ///     </description>
    /// </item>  
    /// <item>
    ///     <attribute>initializeSchema</attribute>
    ///     <description>
    ///         When this boolean attribute is set to true, the provider will try to create the needed tables 
    ///         and stored procedures in the database. This attribute is optional and false by default.
    ///     </description>
    /// </item>  
    /// </list>
    /// The attributes can be specified within the provider configuration. See the example below on how to
    /// use.
    /// </para>
    /// </remarks>
    /// <example>
    /// This example demonstrates how to specify values declaratively for several attributes of the
    /// logging section, which can also be accessed as members of the <see cref="LoggingSection"/> class.
    /// The following configuration file example shows how to specify values declaratively for the
    /// logging section.
    /// <code lang="xml"><![CDATA[
    /// <?xml version="1.0"?>
    /// <configuration>
    ///     <configSections>
    ///         <section name="logging" type="CuttingEdge.Logging.LoggingSection, CuttingEdge.Logging"
    ///             allowDefinition="MachineToApplication" />
    ///     </configSections>
    ///     <connectionStrings>
    ///         <add name="SqlLogging" 
    ///             connectionString="Data Source=.;Integrated Security=SSPI;Initial Catalog=Logging;" />
    ///     </connectionStrings>
    ///     <logging defaultProvider="SqlLoggingProvider">
    ///         <providers>
    ///             <add 
    ///                 name="SqlLoggingProvider"
    ///                 type="CuttingEdge.Logging.SqlLoggingProvider, CuttingEdge.Logging"
    ///                 threshold="Information"
    ///                 connectionStringName="SqlLogging"
    ///                 initializeSchema="True"
    ///                 description="SQL logging provider"
    ///             />
    ///         </providers>
    ///     </logging>
    /// </configuration>
    /// ]]></code>
    /// </example>
    public class SqlLoggingProvider : LoggingProviderBase
    {
        private string connectionString;

        /// <summary>Gets the connection string provided with this provider.</summary>
        /// <value>The connection string.</value>
        public string ConnectionString
        {
            get { return this.connectionString; }
        }

        /// <summary>Initializes the provider.</summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing the provider-specific
        /// attributes specified in the configuration for this provider.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name of the provider has a length of zero.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an attempt is made to call Initialize on a
        /// provider after the provider has already been initialized.</exception>
        /// <exception cref="ProviderException">Thrown when the <paramref name="config"/> contains
        /// unrecognized attributes or when the connectionStringName attribute is not configured properly.</exception>
        public override void Initialize(string name, NameValueCollection config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (string.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "SQL logging provider");
            }

            // Call initialize first.
            base.Initialize(name, config);

            // Performing implementation-specific provider initialization here.
            this.InitializeConnectionString(config);

            bool mustInitializeSchema = this.GetInitializeSchemaAttributeFromConfig(config);

            // Check if the configuration is valid, before initializing the database.
            this.CheckForUnrecognizedAttributes(name, config);

            // When the initialization of the database schema is registered in the configuration file, we
            // execute creation of tables and stored procedures.
            if (mustInitializeSchema)
            {
                this.InitializeDatabaseSchema();
            }
        }

        /// <summary>Initializes the database schema.</summary>
        protected virtual void InitializeDatabaseSchema()
        {
            try
            {
                SqlLoggingHelper.ThrowWhenSchemaAlreadyHasBeenInitialized(this);

                string createScript = SR.SqlLoggingProviderSchemaScripts();
                
                // Split the script in separate operations. SQL Server chokes on the GO statements.
                string[] createScripts = createScript.Split(new string[] { "GO" }, StringSplitOptions.None);

                SqlLoggingHelper.CreateTablesAndStoredProcedures(this, createScripts);
            }
            catch (SqlException ex)
            {
                throw new ProviderException(SR.InitializationOfDatabaseSchemaFailed(this.Name, ex.Message), ex);
            }
        }

        /// <summary>Implements the functionality to log the event.</summary>
        /// <param name="entry">The entry to log.</param>
        /// <returns>An <see cref="Int32"/> with the id of the logged event.</returns>
        protected override object LogInternal(LogEntry entry)
        {
            using (SqlConnection connection = new SqlConnection(this.ConnectionString))
            {
                connection.Open();

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    // Log the message
                    int eventId = 
                        this.SaveEventToDatabase(transaction, entry.Severity, entry.Message, entry.Source);

                    this.SaveExceptionChainToDatabase(transaction, entry.Exception, eventId);

                    transaction.Commit();

                    return eventId;
                }
            }
        }

        /// <summary>Saves the event to database.</summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="severity">The severity of the event.</param>
        /// <param name="message">The message.</param>
        /// <param name="source">The source.</param>
        /// <returns>The database's primary key of the saved event.</returns>
        protected virtual int SaveEventToDatabase(SqlTransaction transaction, LoggingEventType severity, 
            string message, string source)
        {
            using (SqlCommand command =
                new SqlCommand("dbo.logging_AddEvent", transaction.Connection, transaction))
            {
                command.CommandType = CommandType.StoredProcedure;

                SqlLoggingHelper.AddParameter(command, "EventTypeId", SqlDbType.Int, (int)severity);
                SqlLoggingHelper.AddParameter(command, "Message", SqlDbType.NText, message);
                SqlLoggingHelper.AddParameter(command, "Source", SqlDbType.NText, source);

                return (int)command.ExecuteScalar();
            }
        }

        /// <summary>Saves the exception to database.</summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="exception">The exception.</param>
        /// <param name="parentEventId">The parent event id.</param>
        /// <param name="parentExceptionId">The parent exception id.</param>
        /// <returns>The database's primary key of the saved exception.</returns>
        protected virtual int SaveExceptionToDatabase(SqlTransaction transaction, Exception exception,
            int parentEventId, int? parentExceptionId)
        {
            using (SqlCommand command =
                new SqlCommand("dbo.logging_AddException", transaction.Connection, transaction))
            {
                command.CommandType = CommandType.StoredProcedure;

                SqlLoggingHelper.AddParameter(command, "EventId", SqlDbType.Int, parentEventId);
                SqlLoggingHelper.AddParameter(command, "ParentExceptionId", SqlDbType.Int, parentExceptionId);
                SqlLoggingHelper.AddParameter(command, "ExceptionType", SqlDbType.NVarChar, exception.GetType().Name);
                SqlLoggingHelper.AddParameter(command, "Message", SqlDbType.NText, exception.Message);
                SqlLoggingHelper.AddParameter(command, "StackTrace", SqlDbType.NText, exception.StackTrace);

                return (int)command.ExecuteScalar();
            }
        }

        private bool GetInitializeSchemaAttributeFromConfig(NameValueCollection config)
        {
            const string InitializeSchemaAttribute = "initializeSchema";

            string initializeSchema = config[InitializeSchemaAttribute];

            // Remove this attribute from the configuration. This way the provider can spot unrecognized
            // attributes after the initialization process.
            config.Remove(InitializeSchemaAttribute);

            const bool DefaultValueWhenMissing = false;

            return SqlLoggingHelper.ParseBoolConfigValue(this.Name, InitializeSchemaAttribute,
                initializeSchema, DefaultValueWhenMissing);
        }

        private void SaveExceptionChainToDatabase(SqlTransaction transaction, Exception exception,
            int eventId)
        {
            int? parentExceptionId = null;

            while (exception != null)
            {
                parentExceptionId =
                    this.SaveExceptionToDatabase(transaction, exception, eventId, parentExceptionId);

                exception = exception.InnerException;
            }
        }

        private void InitializeConnectionString(NameValueCollection config)
        {
            const string ConnectionStringNameAttribute = "connectionStringName";

            string connectionStringName = config[ConnectionStringNameAttribute];

            // Throw exception when no connectionStringName is provided
            if (string.IsNullOrEmpty(connectionStringName))
            {
                throw new ProviderException(SR.MissingConnectionStringAttribute(this.Name));
            }

            var settings = ConfigurationManager.ConnectionStrings[connectionStringName];

            // Throw exception when connection string is missing from the <connectionStrings> section.
            if (settings == null || String.IsNullOrEmpty(settings.ConnectionString))
            {
                throw new ProviderException(SR.MissingConnectionStringInConfig(connectionStringName));
            }

            // Remove this attribute from the config. This way the provider can spot unrecognized attributes
            // after the initialization process.
            config.Remove(ConnectionStringNameAttribute);

            this.connectionString = settings.ConnectionString;
        }
    }
}