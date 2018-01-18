using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.IO;


namespace NC24_Service
{
    public partial class Service1 : ServiceBase
    {
        bool _processStart;
        string _pathLogs;
        string sCodigoTransaccion;
        System.IO.StreamWriter sw;

        /// <summary>
        /// Constructor
        /// </summary>
        public Service1()
        {
            InitializeComponent();
            this.ServiceName = "NC24_Service";
        }

        /// <summary>
        /// Start de servicio
        /// </summary>
        /// <param name="args">No utilizado</param>
        protected override void OnStart(string[] args)
        {
            StartProcess();
        }
        
        /// <summary>
        /// Proceso inicial de procesamiento. Inicio por Thread
        /// </summary>
        public void StartProcess()
        {
            // Control de procesamiento en true
            _processStart = true;
            _pathLogs = System.Configuration.ConfigurationManager.AppSettings["PathLogs"];

            // Inicio Thread
            System.Threading.Thread _thread = new System.Threading.Thread(new System.Threading.ThreadStart(ProcesarMensajes));
            _thread.Start();
        }
        

        public void ProcesarMensajes()
        {
            string _newArchivo = _pathLogs + "\\NC24Service_Log" + string.Format("{0:yyyyMMdd}", DateTime.Now.Date) + ".txt";
            sw = new StreamWriter(_newArchivo, true);

            sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "-" + " Iniciando procesamiento de mensajes");
            sw.Flush();

            


            AccesoDatos.ConexionDB2 _conDB2 = new AccesoDatos.ConexionDB2();
            AccesoDatos.ConexionDB2.Transaccion _trx = new AccesoDatos.ConexionDB2.Transaccion();
            WSConsumer.RestClient _restClient = new WSConsumer.RestClient();

            //bool _resultado;
            //string _response = "{\"resultado\":\"OK\",\"xmlns\":\"http://sdo.merval/xml\"}";

            //_resultado = _restClient.IngresarOrden(_trx);

            bool _resultadoTrx = false;
            string _mensajeError = string.Empty;

            try
            {
                // Conectar a base DB2
                _conDB2 = new AccesoDatos.ConexionDB2();
                //sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + System.Configuration.ConfigurationManager.ConnectionStrings["DB2Base"].ConnectionString);
                //sw.Flush();
                _conDB2.ConectarBase();
            }
            catch (Exception ex)
            {
                AuditarExcepcion("Sin poder conectar a la base DB2", ex, true);
                sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Excepcion - " + " Sin poder conectar a la base DB2, Mensaje: " + ex.Message);
                sw.Flush();
                this.OnStop();
            }

            while (_processStart)
            {
                try
                {
                    sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "-" + " Obteniendo mensaje...");
                    sw.Flush();
                    // Obtener mensaje a procesar
                    _trx = _conDB2.ObtenerProximoMensaje();
                }
                catch (Exception ex)
                {
                    AuditarExcepcion("Error al obtener mensajes de la base", ex, true);
                    sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Excepcion - " + " Error al obtener mensajes de la base, Mensaje: " + ex.Message);
                    sw.Flush();
                    this.OnStop();
                }

                try
                {
                    sCodigoTransaccion = _trx.CodigoTransaccion;

                    if (sCodigoTransaccion != null)
                    {
                        if (_trx.CodigoTransaccion.ToUpper() == "RP03") //Trx: Insertar Orden
                        {
                            sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "-" + " Mensaje obtenido: " + _trx.CodigoTransaccion.ToUpper());
                            sw.Flush();

                            // Envio a WS Senebi
                            _resultadoTrx = _restClient.IngresarOrden(_trx);
                            //if (!_resultadoTrx)
                            _mensajeError = _restClient._descripcionError;

                            sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "-" + " Resultado procesamiento: " + _resultadoTrx.ToString());
                            sw.Flush();
                        }
                        else if (_trx.CodigoTransaccion.ToUpper() == "RP12") //Trx: Consultar Orden
                        {
                            sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "-" + " Mensaje obtenido: " + _trx.CodigoTransaccion.ToUpper());
                            sw.Flush();

                            // Envio a WS Senebi
                            _resultadoTrx = _restClient.ConsultarOperaciones(_trx);
                            //if (!_resultadoTrx)
                            _mensajeError = _restClient._descripcionError;

                            sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "-" + " Resultado procesamiento: " + _resultadoTrx.ToString());
                            sw.Flush();
                        }
                        else
                        {
                            //Transaccion no reconocida
                            _resultadoTrx = false;
                            _mensajeError = "No existe operatoria para la transaccion recibida";

                            sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "-" + " Resultado procesamiento: " + " Operacion invalida");
                            sw.Flush();
                        }
                    }
                    else
                    {
                        //Transaccion no reconocida
                        _resultadoTrx = false;
                        _mensajeError = "No hay transacciones pendientes";

                        sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "-" + " Resultado procesamiento: " + " No hay operaciones");
                        sw.Flush();
                    }

                }
                catch (Exception ex)
                {
                    AuditarExcepcion("Problemas de conexion con el WS", ex, true);
                    sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Excepcion - " + " Problemas de conexion con el WS, Mensaje: " + ex.Message);
                    sw.Flush();

                    _conDB2.ActualizarTransaccion(_trx.NumeroSecuencia, false, "Error de conexion con el WS");
                    this.OnStop();
                }

                try
                {
                    if (sCodigoTransaccion != null)
                    {
                        sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "-" + " Actualizo transaccion y audito mensaje enviado");
                        sw.Flush();
                        //Actualizo estado de transaccion obtenida de la base
                        _conDB2.ActualizarTransaccion(_trx.NumeroSecuencia, _resultadoTrx, _mensajeError);
                        //Audito mensaje como enviado
                        _conDB2.AuditarTransaccion(_trx.NumeroSecuencia, "E", _trx.TramaMensaje);
                    }
                }
                catch (Exception ex)
                {
                    AuditarExcepcion("Sin poder actualizar el mensaje enviado", ex, true);
                    sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Excepcion - " + " Sin poder actualizar el mensaje enviado, Mensaje: " + ex.Message);
                    sw.Flush();
                    this.OnStop();
                    
                }

                try
                {
                    if (sCodigoTransaccion != null)
                    {
                        // Inserto respuesta de Senebi en Tabla de Operaciones ya sea Ok o no
                        if (_trx.CodigoTransaccion.ToUpper() == "RP03")
                        {
                            string _codOpe = string.Empty;
                            string _plazo = string.Empty;
                            //_mensajeError += _restClient._respuestaIO.ordenBilateral.
                            sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "-" + " Actualizo operacion ALTA DE ORDEN");
                            sw.Flush();
                            try
                            {
                                if (_resultadoTrx)
                                {
                                    _codOpe = "0";
                                    _plazo = _restClient._respuestaIO.ordenBilateral[0].tipoVenc; 
                                    if (_restClient._respuestaIO.ordenBilateral[0].estado == "OPERADA")
                                    {
                                        _codOpe = _restClient._respuestaIO.ordenBilateral[0].operacion;
                                        //ordenBilateral.id = "0";
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Excepcion - " + " al cargar id y tipoVenc " + ex.Message);
                                sw.Flush();
                            }
                            try
                            {
                                _conDB2.ActualizarOperacion(_trx.NumeroSecuencia, _codOpe, _plazo, _mensajeError, _resultadoTrx);
                            }
                            catch (Exception ex)
                            {
                                sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Excepcion - " + " al hacer update a la Base " + ex.Message);
                                sw.Flush();
                            }
                        }


                        if (_trx.CodigoTransaccion.ToUpper() == "RP12")
                        {
                            sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "-" + " Actualizo operaciones CONSULTA DE ORDENES");
                            sw.Flush();
                            if (_restClient._respuestaIOVacia.resultado == "false")
                            {
                                foreach (var ordenBilateral in _restClient._respuestaIO.ordenBilateral)
                                {

                                    string _codOpe = string.Empty;
                                    string _plazo = string.Empty;
                                    string _nroSecuencia = ordenBilateral.idOrigen;

                                    if (ordenBilateral.estado == "RECHAZADA")
                                    {
                                        _mensajeError = ordenBilateral.resultadoRuteo + " - " + ordenBilateral.observacionesRuteo;
                                        //ordenBilateral.id = "0";
                                    }
                                    else if (ordenBilateral.estado == "DESCONOCIDO")
                                    {
                                        _mensajeError = ordenBilateral.resultadoRuteo + " - " + ordenBilateral.observacionesRuteo;
                                        //ordenBilateral.id = "0";
                                    }
                                    else
                                    {
                                        _mensajeError = string.Empty;
                                    }
                                        

                                    if (_resultadoTrx)
                                    {
                                        try
                                        {
                                            _codOpe = "0";
                                            _plazo = ordenBilateral.tipoVenc;
                                            if (ordenBilateral.estado == "OPERADA")
                                            {
                                                _codOpe = ordenBilateral.operacion;
                                                //ordenBilateral.id = "0";
                                            }

                                        }
                                        catch (Exception ex)
                                        {
                                            sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Excepcion - " + " al crear respuestaIO " + ex.Message);
                                            sw.Flush();
                                        }
                                    }

                                    try
                                    {
                                        _conDB2.ActualizarOperacionRP012(_nroSecuencia, _codOpe, _plazo, _mensajeError, _resultadoTrx);
                                    }
                                    catch (Exception ex)
                                    {
                                        sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Excepcion - " + " al hacer update a la Base " + ex.Message);
                                        sw.Flush();
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AuditarExcepcion("Sin poder insertar orden", ex, true);
                    sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Excepcion - " + " Sin poder insertar la orden, Mensaje: " + ex.Message + " " + ex.InnerException + " " + ex.StackTrace + " " + ex.TargetSite);
                    sw.Flush();
                    this.OnStop();
                }

                int iSleep = Int32.Parse(System.Configuration.ConfigurationManager.AppSettings["SleepService"]);
                System.Threading.Thread.Sleep(iSleep);
            }

            try
            {
                sw.Close();
                _conDB2.DesconectarBase();
            }
            catch (Exception ex)
            {
                AuditarExcepcion("No se puede desconectar a la base DB2", ex, true);
            }

        }

        /// <summary>
        /// Ingreso de eventos en visor de sucesos
        /// </summary>
        /// <param name="msg">Detalle de donde se genera el evento</param>
        /// <param name="ex">Excepcion</param>
        /// <param name="EsError">Indica si se tiene que grabar el evento como error o Warning</param>
        public void AuditarExcepcion(string msg, Exception ex, bool EsError)
        {
            string _err = GetMensajesEx(ex);

            if(EsError)
                System.Diagnostics.EventLog.WriteEntry("NC24_Service", msg + "\r\n" + "\r\n" + _err, EventLogEntryType.Error);
            else
                System.Diagnostics.EventLog.WriteEntry("NC24_Service", msg + "\r\n" + "\r\n" + _err, EventLogEntryType.Warning);
        }

        /// <summary>
        /// Obtiene detalle de la excepcion
        /// </summary>
        /// <param name="ex">Excepcion obtenida</param>
        /// <returns>Detalle de la execion en formato string</returns>
        private static string GetMensajesEx(Exception ex)
        {
            string _err = ex.Message;
            if (ex.InnerException != null)
            {
                _err += "\r\n";
                _err += GetMensajesEx(ex.InnerException);
            }

            return _err;
        }

        /// <summary>
        /// Envio de mails. Se obtiene server, remitente y destinatarios desde app_config.
        /// </summary>
        /// <param name="asunto">string que viaja en el asunto del mensaje</param>
        /// <param name="body">cuerpo del mensaje de mail</param>
        public void EnviarMail(string asunto, string body)
        {
            try
            {
                string[] _dest = System.Text.RegularExpressions.Regex.Split(System.Configuration.ConfigurationManager.AppSettings["SmtpDestinatarios"], ";");

                for (int intI = 0; intI < _dest.Length; intI++)
                {
                    System.Net.Mail.SmtpClient objMail = new System.Net.Mail.SmtpClient();
                    objMail.Host = System.Configuration.ConfigurationManager.AppSettings["SmtpServer"];
                    objMail.Port = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["SmtpPort"]);
                    objMail.EnableSsl = (System.Configuration.ConfigurationManager.AppSettings["SmtpSSL"].ToUpper() == "S");
                    objMail.UseDefaultCredentials = true;
                    System.Net.Mail.MailMessage objMsg = new System.Net.Mail.MailMessage(System.Configuration.ConfigurationManager.AppSettings["SmtpRemitente"], _dest[intI], asunto, body + "<p>&nbsp;</p> <p>&nbsp;</p>");
                    objMsg.IsBodyHtml = true;
                    objMail.Send(objMsg);
                }
            }
            catch (Exception ex)
            {
                AuditarExcepcion("No se pudo enviar el Mail", ex, false);
            }
        }

        /// <summary>
        /// Stop de servicio
        /// </summary>
        protected override void OnStop()
        {
            _processStart = false;
            if (sw != null)
            {
                sw.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Cierre de servicio" );
                sw.Close();
            }
        }
    }
}
