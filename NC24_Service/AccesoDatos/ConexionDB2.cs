using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IBM.Data.DB2.iSeries;
using System.IO;

namespace NC24_Service.AccesoDatos
{
    class ConexionDB2
    {
        System.IO.StreamWriter _archivoLog;
        /// <summary>
        /// Conecta a la base de datos dejando la misma abierta
        /// </summary>
        public void ConectarBase()
        {
            _myConn = new iDB2Connection(System.Configuration.ConfigurationManager.ConnectionStrings["DB2Base"].ConnectionString);
            _myConn.Open();
        }


        /// <summary>
        /// Desconecta de la base de datos
        /// </summary>
        public void DesconectarBase()
        {
            if (_myConn != null)
            {
                _myConn.Close();
            }
        }

        /// <summary>
        /// Obtiene los datos necesarios para poder enviar la transaccion a SENEBI
        /// </summary>
        /// <returns>Clase Transaccion, array de string</returns>
        public Transaccion ObtenerProximoMensaje()
        {
            string _pathAudit = System.Configuration.ConfigurationManager.AppSettings["PathLogs"];
            string _newFile = _pathAudit + "\\Datos_log" + string.Format("{0:yyyyMMdd}", DateTime.Now.Date) + ".txt";
            _archivoLog = new StreamWriter(_newFile, true);


            iDB2Transaction _trans = _myConn.BeginTransaction();
            iDB2Command _myDB2Command = _myConn.CreateCommand();
            iDB2DataReader _reader;
            Transaccion _transaccion = new Transaccion();

            string _myQuery = "SELECT V4CTRX, V4NSEQ, V4DATO, V4ESTA, V4FALT, V4HALT ";
            _myQuery += "FROM CCVREQ ";
            _myQuery += "WHERE (V4FALT = " + Convert.ToDecimal(string.Format("{0:yyyyMMdd}", DateTime.Now)).ToString() + ") AND (V4ESTA = 'P') ";
            _myQuery += "ORDER BY V4FALT, V4HALT FETCH FIRST 1 ROW ONLY";
            
            _myDB2Command.CommandText = _myQuery;
            _myDB2Command.Transaction = _trans;
            _reader = _myDB2Command.ExecuteReader();

            _archivoLog.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Query a la base = " + _myQuery);
            _archivoLog.Flush();

            if (_reader.Read())
            {
                try
                {
                    _archivoLog.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- CodigoTransaccion = " + _reader.GetString(0));
                    _archivoLog.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- NumeroSecuencia = " + _reader.GetString(1));
                    _archivoLog.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- TramaMensaje = " + _reader.GetString(2));
                    _archivoLog.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- EstadoMensaje = " + _reader.GetString(3));
                    _archivoLog.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- FechaAlta = " + _reader.GetString(4));
                    _archivoLog.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- HoraAlta = " + _reader.GetString(5));
                    _archivoLog.Flush();

                    _transaccion.CodigoTransaccion = _reader.GetString(0);
                    _transaccion.NumeroSecuencia = _reader.GetString(1);
                    _transaccion.TramaMensaje = _reader.GetString(2);
                    _transaccion.EstadoMensaje = _reader.GetString(3);
                    _transaccion.FechaAlta = _reader.GetString(4);
                    _transaccion.HoraAlta = _reader.GetString(5);
                }
                catch (Exception ex)
                {
                    _archivoLog.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + " - Excepcion " + ex.Message + " " + ex.InnerException + " " + ex.StackTrace + " " + ex.TargetSite);
                    _archivoLog.Flush();
                }
                
            }
            else
            {
                _archivoLog.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + " - Sin resultados");
                _archivoLog.Flush();
            }
            _reader.Close();
            _trans.Commit();

            _archivoLog.Close();
            return _transaccion;
        }

        /// <summary>
        /// Actualiza estado y mensaje de una operacion obtenida
        /// </summary>
        /// <param name="NumeroSecuencia">Numero de secuencia de la operacion</param>
        /// <param name="Resultado">P:Procesada - R:Error</param>
        /// <param name="MensajeError">Descripcion del error en caso que corresponda</param>
        public void ActualizarTransaccion(string NumeroSecuencia, bool Resultado, string MensajeError)
        {
            string _estado = string.Empty;
            if (Resultado)
                _estado = "C";
            else
                _estado = "R";

            iDB2Transaction _trans = _myConn.BeginTransaction();
            iDB2Command _myDB2Command = _myConn.CreateCommand();

            string _myQuery = "UPDATE CCVREQ ";
            _myQuery += "SET V4FENV = " + string.Format("{0:yyyyMMdd}", DateTime.Now);
            _myQuery += " , V4HENV = " + string.Format("{0:HHmmss}", DateTime.Now);
            _myQuery += " , V4ESTA = '" + _estado + "'";
            _myQuery += " , V4MENS = '" + MensajeError + "' ";
            _myQuery += "WHERE  V4NSEQ = " + NumeroSecuencia;

            
            _myDB2Command.CommandText = _myQuery;

            _myDB2Command.Transaction = _trans;
            _myDB2Command.ExecuteNonQuery();
            _trans.Commit();
            
        }

        /// <summary>
        /// Audita la transaccion en la tabla de auditoria
        /// </summary>
        /// <param name="NumeroSecuencia">Numero de secuencia de la transaccion</param>
        /// <param name="TipoMensaje">E: Enviado - R: Recibido</param>
        /// <param name="CampoDato">Detalle de la transaccion</param>
        public void AuditarTransaccion(string NumeroSecuencia, string TipoMensaje, string CampoDato)
        {
            iDB2Transaction _trans = _myConn.BeginTransaction();
            iDB2Command _myDB2Command = _myConn.CreateCommand();

            string _myQuery = "INSERT INTO CCVAUD (V7FECH, V7HORA, V7TMSJ, V7NSEQ, V7BUFF) ";
            _myQuery += "VALUES(" + string.Format("{0:yyyyMMdd}", DateTime.Now) + ", ";
            _myQuery += string.Format("{0:HHmmss}", DateTime.Now) + ", '";
            _myQuery += TipoMensaje + "', " + NumeroSecuencia + ", '" + CampoDato + "')";

            _myDB2Command.CommandText = _myQuery;

            _myDB2Command.Transaction = _trans;
            _myDB2Command.ExecuteNonQuery();
            _trans.Commit();
        }

        /// <summary>
        /// Actualiza operacion luego del alta recibida desde WS.
        /// </summary>
        /// <param name="NumeroSecuencia">Numero de secuencia de la transaccion</param>
        /// <param name="CodigoOperacion">Codigo de operacion recibido por CV</param>
        /// <param name="Plazo">Plazo actualizado</param>
        /// <param name="MensajeError">Para alta fallida: descripcion</param>
        /// <param name="Resultado">Resultado del proceso con CV, para saber que update realizar</param>
        public void ActualizarOperacion(string NumeroSecuencia, string CodigoOperacion, string Plazo, string MensajeError, bool Resultado)
        {
            string _pathAudit = System.Configuration.ConfigurationManager.AppSettings["PathLogs"];
            string _newFile = _pathAudit + "\\Datos_log" + string.Format("{0:yyyyMMdd}", DateTime.Now.Date) + ".txt";
            _archivoLog = new StreamWriter(_newFile, true);

            iDB2Transaction _trans = _myConn.BeginTransaction();
            iDB2Command _myDB2Command = _myConn.CreateCommand();

            if (Plazo == "CI")
            {
                Plazo = "00";
            }

            string _myQuery = "UPDATE CCVOPE SET ";

            if (Resultado)
                _myQuery += "V3IOPE = " + CodigoOperacion + ", V3PLAZ = " + Plazo + ", ";
            
            _myQuery += "V3MENS = '" + MensajeError + "'";

            _myQuery += " WHERE  V3NSEQ = " + NumeroSecuencia ;

            _archivoLog.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Query a la base = " + _myQuery);
            _archivoLog.Flush();

            _myDB2Command.CommandText = _myQuery;
            _myDB2Command.Transaction = _trans;
            _myDB2Command.ExecuteNonQuery();
            _trans.Commit();
            _archivoLog.Close();
        }

        /// <summary>
        /// Actualiza operacion luego del alta recibida desde WS.
        /// </summary>
        /// <param name="NumeroSecuencia">Numero de secuencia de la transaccion</param>
        /// <param name="CodigoOperacion">Codigo de operacion recibido por CV</param>
        /// <param name="Plazo">Plazo actualizado</param>
        /// <param name="MensajeError">Para alta fallida: descripcion</param>
        /// <param name="Resultado">Resultado del proceso con CV, para saber que update realizar</param>
        public void ActualizarOperacionRP012(string NumeroSecuencia, string CodigoOperacion, string Plazo, string MensajeError, bool Resultado)
        {
            string _pathAudit = System.Configuration.ConfigurationManager.AppSettings["PathLogs"];
            string _newFile = _pathAudit + "\\Datos_log" + string.Format("{0:yyyyMMdd}", DateTime.Now.Date) + ".txt";
            _archivoLog = new StreamWriter(_newFile, true);

            iDB2Transaction _trans = _myConn.BeginTransaction();
            iDB2Command _myDB2Command = _myConn.CreateCommand();

            if (Plazo == "CI")
            {
                Plazo = "00";
            }

            string _myQuery = "UPDATE CCVOPE SET ";

            if (Resultado)
                _myQuery += "V3IOPE = " + CodigoOperacion + ", V3PLAZ = " + Plazo + ", ";
            
            _myQuery += "V3MENS = '" + MensajeError + "'";

            _myQuery += " WHERE  V3NSEQ = " + NumeroSecuencia;

            _archivoLog.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Query a la base RP12 = " + _myQuery);
            _archivoLog.Flush();

            _myDB2Command.CommandText = _myQuery;
            _myDB2Command.Transaction = _trans;
            _myDB2Command.ExecuteNonQuery();
            _trans.Commit();
            _archivoLog.Close();
        }

        private iDB2Connection _myConn;
        public class Transaccion
        {
            public string CodigoTransaccion { get; set; }
            public string NumeroSecuencia { get; set; }
            public string TramaMensaje { get; set; }
            public string EstadoMensaje { get; set; }
            public string FechaAlta { get; set; }
            public string HoraAlta { get; set; }

        }

    }
}
