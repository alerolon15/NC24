using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Net;
using System.IO;
using System.Web.Script.Serialization;

namespace NC24_Service.WSConsumer
{
    class RestClient
    {
        /// <summary>
        /// Enviar solicitud de ingreso de orden (para alta de Tipo C o V) hacia CV.
        /// Se arma un JSon para el request a partir de una clase ya definida.
        /// Se actualizan 2 variables publicas que se van a utilizar en Service1:
        /// _resultado: Diccionario string, string donde esta toda la respuesta de CV.
        /// _descripcionError: Obtenido desde CV.
        /// </summary>
        /// <param name="TransaccionLocal">Clase a partir de la cual se arma el JSON</param>
        /// <returns>true: Se dio de alta la orden - false: No.</returns>
        public bool IngresarOrden(AccesoDatos.ConexionDB2.Transaccion TransaccionLocal)
        {
            string _pathAuditoria = System.Configuration.ConfigurationManager.AppSettings["PathLogs"];
            string _newFileAudit = _pathAuditoria + "\\WS_log" + string.Format("{0:yyyyMMdd}", DateTime.Now.Date) + ".txt";
            System.IO.StreamWriter _archivoLogAuditoria = new StreamWriter(_newFileAudit, true);


            //ToDo: Verificar armado de mensaje
            IngresarOrdenJSON _requestJson = new IngresarOrdenJSON();
            _requestJson.agente = Int32.Parse(TransaccionLocal.TramaMensaje.Substring(0, 4)).ToString();
            _requestJson.idOrigen = TransaccionLocal.NumeroSecuencia;
            _requestJson.fechaOrigen = TransaccionLocal.FechaAlta;
            _requestJson.ejecucion = "SINCRONICA";
            _requestJson.tipo = TransaccionLocal.TramaMensaje.Substring(4, 1);
            _requestJson.instrumento = TransaccionLocal.TramaMensaje.Substring(6, 5);
            _requestJson.cantidad = TransaccionLocal.TramaMensaje.Substring(11, 11);
            _requestJson.cantidad += ".";
            _requestJson.cantidad += TransaccionLocal.TramaMensaje.Substring(22, 7);
            _requestJson.precio = TransaccionLocal.TramaMensaje.Substring(29, 8);
            _requestJson.precio += ".";
            _requestJson.precio += TransaccionLocal.TramaMensaje.Substring(37, 3);
            _requestJson.formaOp = TransaccionLocal.TramaMensaje.Substring(5, 1);
            _requestJson.tipoVenc = TransaccionLocal.TramaMensaje.Substring(44, 2);
            _requestJson.codigoAgente = Int32.Parse(TransaccionLocal.TramaMensaje.Substring(0, 4)).ToString();
            _requestJson.agenteCtpte = Int32.Parse(TransaccionLocal.TramaMensaje.Substring(46, 4)).ToString();

            if (_requestJson.tipoVenc == "00")
            {
                _requestJson.tipoVenc = "CI";
            }

            if (_requestJson.agenteCtpte == "0")
            {
                _requestJson.agenteCtpte = "109";
            }


            string strJson = new JavaScriptSerializer().Serialize(_requestJson);

            _archivoLogAuditoria.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Request = " + strJson);
            _archivoLogAuditoria.Flush();
            // Envio hacia Senebi 
            string _response = RealizarEnvio(strJson, "ingresarOrdenBilateral");

            _archivoLogAuditoria.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Response = " + _response);
            _archivoLogAuditoria.Close();

            // Lectura de JSON
            _resultado = ProcesarRespuesta(_response);

            if (_resultado)
            {
                if (_respuestaIO.resultado != null)
                {
                    if (_respuestaIO.ordenBilateral[0].estado == "RECHAZADA")
                    {
                        _descripcionError = _respuestaIO.ordenBilateral[0].resultadoRuteo + " - " + _respuestaIO.ordenBilateral[0].observacionesRuteo;
                        return false;
                    }
                    _descripcionError = string.Empty;
                    return true;
                }
                else if (_respuestaIOError.resultado != null)
                {
                    _descripcionError = _respuestaIOError.codigo;
                    _descripcionError += " - ";
                    _descripcionError += _respuestaIOError.observaciones;
                    return false;
                }
                else
                {
                    _descripcionError = "0014";
                    _descripcionError += " - ";
                    _descripcionError += "SIN CONEXION CON SENEBI";
                    return false;
                }
            }
            else
            {
                _descripcionError = "0014";
                _descripcionError += " - ";
                _descripcionError += "SIN CONEXION CON SENEBI";
                return false;
            }
            


        }

        /// <summary>
        /// Se consultan todas las operaciones de un agente entre un rango de numeros de orden
        /// Se arma un JSon para el request a partir de una clase ya definida.
        /// Se actualizan 2 variables publicas que se van a utilizar en Service1:
        /// _resultado: Diccionario string, string donde esta toda la respuesta de CV.
        /// _descripcionError: Obtenido desde CV.
        /// </summary>
        /// <param name="TransaccionLocal">Clase que se utiliza para armar el JSON</param>
        /// <returns>true: Se obtuvieron operaciones - false: No.</returns>
        public bool ConsultarOperaciones(AccesoDatos.ConexionDB2.Transaccion TransaccionLocal)
        {
            string _pathAuditoria = System.Configuration.ConfigurationManager.AppSettings["PathLogs"];
            string _newFileAudit = _pathAuditoria + "\\WS_log" + string.Format("{0:yyyyMMdd}", DateTime.Now.Date) + ".txt";
            System.IO.StreamWriter _archivoLogAuditoria = new StreamWriter(_newFileAudit, true);
            string _response = string.Empty;
            try
            {
                ConsultarOrdenJSON _request = new ConsultarOrdenJSON();
            
                _request.codigoAgente = TransaccionLocal.TramaMensaje.Substring(1 ,3);
                //_request.ordenDesde = TransaccionLocal.TramaMensaje.Substring(5, 2);
                _request.idOrigenDesde = 0;
                //_request.idOrigenHasta = _request.idOrigenDesde;
                //_request.ordenHasta = (Convert.ToDecimal(TransaccionLocal.TramaMensaje.Substring(5, 2)) + 10).ToString();

                string strJson = new JavaScriptSerializer().Serialize(_request);

                _archivoLogAuditoria.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Request = " + strJson);
                _archivoLogAuditoria.Flush();
            
                _response = RealizarEnvio(strJson, "consultarOrdenesBilateral");
            
                // Envio hacia Senebi
            

                _archivoLogAuditoria.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- Response = " + _response);
                _archivoLogAuditoria.Flush();
                

                // Lectura de JSON
                _resultado = ProcesarRespuestaRP12(_response);
            }
            catch (Exception ex)
            {
                _archivoLogAuditoria.WriteLine(string.Format("{0:dd/MM/yyyy HH:mm:ss}", DateTime.Now) + "- No se pudo realizar el envio = " + ex.Message + " " + ex.StackTrace);
                _archivoLogAuditoria.Flush();
            }
            finally { _archivoLogAuditoria.Close(); }

            if (_resultado)
            {
                if (_respuestaIO.resultado != null)
                {
                    //if (_respuestaIO.ordenBilateral[0].estado == "DESCONCIDO")
                    //{
                    //    _descripcionError = _respuestaIO.ordenBilateral[0].resultadoRuteo + " - " + _respuestaIO.ordenBilateral[0].observacionesRuteo;
                    //    return false;
                    //}
                    _descripcionError = string.Empty;
                    return true;
                }
                else if (_respuestaIOError.resultado != null)
                {
                    _descripcionError = _respuestaIOError.codigo;
                    _descripcionError += " - ";
                    _descripcionError += _respuestaIOError.observaciones;
                    return false;
                }
                else if (_respuestaIOVacia.resultado != null)
                {
                    _descripcionError = string.Empty;
                    return true;
                }
                else
                {
                    _descripcionError = "0014";
                    _descripcionError += " - ";
                    _descripcionError += "SIN CONEXION CON SENEBI";
                    return false;
                }
            }
            else
            {
                _descripcionError = "0014";
                _descripcionError += " - ";
                _descripcionError += "SIN CONEXION CON SENEBI";
                return false;
            }
        }

        /// <summary>
        /// Realiza el envio del request al Servicio Rest
        /// Tipo de envio: Post
        /// Tipo de Datos: JSON
        /// TimeOut: Por default se va a tomar 1 minuto
        /// </summary>
        /// <param name="JsonRequest">JSON a enviar</param>
        /// <param name="Metodo">Metodo de WS a ejecutar</param>
        /// <returns>JSON de respuesta</returns>
        private string RealizarEnvio(string JsonRequest, string Metodo)
        {
            var postString = JsonRequest;
            byte[] data = UTF8Encoding.UTF8.GetBytes(postString);
            string respuesta = string.Empty;

            string _servidor = ConfigurationManager.AppSettings["Servidor"];
            _servidor += "/" + Metodo;
            
            try
            {
                WebRequest webReq = WebRequest.Create(_servidor);
                webReq.Timeout = 60 * 1000; // Time Out de 1 minuto
                webReq.Method = "POST";
                webReq.ContentType = "application/json; charset=utf-8";
                webReq.ContentLength = data.Length;
                Stream stream = webReq.GetRequestStream();
                stream.Write(data, 0, data.Length);
                stream.Close();
                WebResponse response = webReq.GetResponse();
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    respuesta = reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                throw ex;              
            }
            

            return respuesta;
        }

        /// <summary>
        /// Transforma respuesta JSON a Diccionario: string, string para poder parsearla
        /// </summary>
        /// <param name="Respuesta">JSON recibido por Senebi</param>
        /// <returns>Diccionario string string con JSON de respuesta</returns>
        public bool ProcesarRespuesta(string Respuesta)
        {
            _respuestaIOVacia = new IngresarOrdenRespuestaVacia();
            _respuestaIOVacia.resultado = "false";
            _respuestaIO = new IngresarOrdenRespuesta();
            JavaScriptSerializer _jscript = new JavaScriptSerializer();
            if (Respuesta.IndexOf("ordenBilateral") >= 0)
            {
                if (Respuesta.IndexOf(":{") >= 0) {
                    Respuesta = Respuesta.Replace(":{", ":[{");
                    Respuesta = Respuesta.Replace("}}", "}]}");
                }
                
                var _result = _jscript.Deserialize<IngresarOrdenRespuesta>(Respuesta);
                _respuestaIO = _result;
                if (_result != null)
                    return true;
                
            }
            else
            {
                 var _result = _jscript.Deserialize<IngresarOrdenRespuestaError>(Respuesta);
                 _respuestaIOError = _result;
                 if (_result != null)
                     return true;
            }

            return false ;
        }

        /// <summary>
        /// Transforma respuesta JSON a Diccionario: string, string para poder parsearla
        /// </summary>
        /// <param name="Respuesta">JSON recibido por Senebi</param>
        /// <returns>Diccionario string string con JSON de respuesta</returns>
        public bool ProcesarRespuestaRP12(string Respuesta)
        {
            _respuestaIOVacia = new IngresarOrdenRespuestaVacia();
            _respuestaIOVacia.resultado = "false";
            _respuestaIO = new IngresarOrdenRespuesta();
            _respuestaIOError = new IngresarOrdenRespuestaError();
            JavaScriptSerializer _jscript = new JavaScriptSerializer();
            if (Respuesta.IndexOf("ordenBilateral") >= 0)
            {
                if (Respuesta.IndexOf(":{") >= 0)
                {
                    Respuesta = Respuesta.Replace(":{", ":[{");
                    Respuesta = Respuesta.Replace("}}", "}]}");
                }

                var _result = _jscript.Deserialize<IngresarOrdenRespuesta>(Respuesta);
                _respuestaIO = _result;
                if (_result != null)
                    return true;

            }
            else
            {
                if (Respuesta.IndexOf("observaciones") >= 0)
                {
                    var _result = _jscript.Deserialize<IngresarOrdenRespuestaError>(Respuesta);
                    _respuestaIOError = _result;
                    if (_result != null)
                        return true;
                    
                }
                else
                {
                    var _result = _jscript.Deserialize<IngresarOrdenRespuestaVacia>(Respuesta);
                    _respuestaIOVacia.resultado = "true";
                    if (_result != null)
                        return true;
                }
            }

            return false;
        }

        public string _descripcionError;
        public IngresarOrdenRespuesta _respuestaIO;
        public IngresarOrdenRespuestaError _respuestaIOError;
        public IngresarOrdenRespuestaVacia _respuestaIOVacia;
        public bool _resultado;
        class IngresarOrdenJSON
        {
            public string idOrigen { get; set; }
            public string fechaOrigen { get; set; }
            public string agente { get; set; }
            public string agenteCtpte { get; set; }
            public string tipo { get; set; }
            public string ejecucion { get; set; }
            public string instrumento { get; set; }
            public string cantidad { get; set; }
            public string precio { get; set; }
            public string formaOp { get; set; }
            public string tipoVenc { get; set; }
            public string codigoAgente { get; set; }
        }
        class ConsultarOrdenJSON
        {
            public string codigoAgente { get; set; }
            public int idOrigenDesde { get; set; }
            //public int idOrigenHasta { get; set; }
        }

        public class IngresarOrdenRespuesta
        {
            public string resultado;
            public List<OrdenDetalleIO> ordenBilateral;
        }
        public class OrdenDetalleIO
        {
            public string tipo;
            public string instrumento;
            public string cantidad;
            public string precio;
            public string formaOp;
            public string tipoVenc;
            public string comitente;
            public string cuit;
            public string carteraPropia;
            public string idOrigen;
            public string agente;
            public string id;
            public string usuario;
            public string recepcion;
            public string estado;
            public string resultadoRuteo;
            public string observacionesRuteo;
            public string oferta;
            public string operador;
            public string operacion;
            public string fechaOrigen;
        }

        public class IngresarOrdenRespuestaError
        {
            public string observaciones;
            public string resultado;
            public string codigo;
        }

        public class IngresarOrdenRespuestaVacia
        {
            public string resultado;
        }


    }
}
