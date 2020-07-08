using Android.Graphics;
using Plugin.Media;
using Plugin.Media.Abstractions;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Xamarin.Forms;

namespace DemoCamera
{
    public partial class MainPage : ContentPage
    {
        // LA VARIABLE READY LO QUE HACE ES EVITAR QUE SE PIDA UNA NUEVA SOLICITUD SI UNA PREVIA NO TERMINO Y EVITAR ASI SATURACION Y LAGS
        Boolean Ready = true;

        // --------------------------------------------------------------------------------------------------------------------------------------------
        // ACA VAN LOS DATOS DEL SERVIDOR, LOS MISMO SE PUEDEN OBTENER DE LA APLICACION DE ADMINISTRACION O DESDE LA WEB DE ADMINISTRACION DEL SERVICIO


        String ServerAPIURL = "http://server1.proyectohorus.com.ar";    // << modificar por la URL del servidor en formato http://.........../produccion
        String ServerUser = "";                                         // << modificar por el usuario creado en el administrador de la API
        String ServerPassword = "";                                     // << modificar por la clave creada en el adminisrador de la API
        String Profile = "";                                            // << escribir el UUID correspondiente al perfil creado en el administrador de la API, el mismo debe estar bajo el miosmo usuario arriba escrito
        String EndPointURL = "/api/v2/functions/object/detection";      // << ACA SE DEBE ESCRIBIR EL ENDPOINT EL CUAL DEBE SER EL MISMO DEL PERFIL SELECCIONADO 
        // --------------------------------------------------------------------------------------------------------------------------------------------


        // A LA VARIABLE TOKEN VA A IR A VARIAI ESA MEDIA LLAVE QUE SE REQUIERE PARA INTERACTUAR CON EL SERVICIO
        // EL TOKEN TIENE UNA DURACION DE 48HS, LUEGO SE DEBERA SOLICITAR UN NUEVO TOKE.
        String token = "";

        public static Bitmap scaleDown(Bitmap realImage, float maxImageSize, Boolean filter)
        {
            // LA API SOLO RECIBE IMAGENES DE HASTA 1920X180 POR LO CUAL USAMOS ESTA FUNCION PARA REESCALAR LA IMAGEN EN CASO QUE LA MISMA SUPERE ESAS MEDIDAS
            float ratio = Math.Min(
                    (float)maxImageSize / realImage.Width,
                    (float)maxImageSize / realImage.Height);

            Int32 width = Convert.ToInt32(Math.Round((float)ratio * realImage.Width));
            Int32 height = Convert.ToInt32(Math.Round((float)ratio * realImage.Height));

            Bitmap newBitmap = Bitmap.CreateScaledBitmap(realImage, width, height, filter);

            return newBitmap;
        }

        public byte[] imageToByteArray(Android.Graphics.Bitmap imageIn)
        {
            // EN ESTA FUNCION LO QUE HACEMOS ES CONVERTIR UNA IMAGEN A JPG Y LUEGO A BYTE ARRAY
            MemoryStream ms = new MemoryStream();

            Android.Graphics.Bitmap scaledThumb = Android.Graphics.Bitmap.CreateScaledBitmap(imageIn, imageIn.Width, imageIn.Height, false);
            scaledThumb.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg, 90, ms);

            return ms.ToArray();
        }

        public async void GetToken()
        {
            // EN ESTA FUNCION OBTENGO EL TOKEN QUE ME PERMITIRA INTERACTUAR CON EL SERVICIO, ALGO ASI COMO EL USUARIO Y LA CONTRASEÑA
            if (token == "")
            {
                Ready = false;

                // DECLARAMOS LAS FUNCIONES NECESARIAS PARA ACCEDER AL SERVICIO ON LINE
                HttpClient httpClient = new HttpClient();
                MultipartFormDataContent form = new MultipartFormDataContent();
                HttpResponseMessage response;

                // DEFINIMOS LAS VARIABLES DE LA FUNCION
                form.Add(new StringContent(ServerUser), "user"); // user: es la primera de tres variables que toma "gettoken"
                form.Add(new StringContent(ServerPassword), "password"); // password es la segunda de tres variables que toma "gettoken"
                form.Add(new StringContent(Profile), "profileuuid"); // profileuuid: es la tercera de tres variables que toma "gettoken"


                // ENVIO LOS DATOS AL SERVIDOR
                response = await httpClient.PostAsync(ServerAPIURL + "/api/v2/functions/login", form);

                response.EnsureSuccessStatusCode();
                httpClient.Dispose();


                // COMO DEFINIMOS A outformat COMO pipe LA CADENA DE RETORNO SERA UN STRING SEPARADO POR "|", POR LO CUAL PARA OBTENER A CADA VALOR POR SEPARADO USAREMOS SPLIT('|'). 
                String[] RecivedMatrix = response.Content.ReadAsStringAsync().Result.Split('|');

                // SI OBTENGO UNA RESPUESTA CON EL CODIGO 200 EN LA POSICION 0 DEL STREAM DE RESPUESTA SIGNIFICA QUE EN LA POSICION 1 SE ENTREGO EL TOKEN
                if (RecivedMatrix[0] == "200")
                {
                    token = RecivedMatrix[1];
                }
                else
                {
                    token = "";
                }

                Ready = true;
            }
        }

        private async void Upload(Android.Graphics.Bitmap imageIn)
        {
            // EN ESTA FUNCION HACEMOS EL UPLOAD DE LA IMAGEN A EVALUAR Y MUESTRA EL RESULTADO
            try
            {
                // LO PRIMERO QUE HACEMOS ES CONSULTAR A LA VARIABLE READY PARA VER SI AUN HAY UNA RESPUESTA POR OBTENER DEL SERVIDOR ANTES DE ENVIAR UNA NUEVA CONSULTA
                if (Ready == true)
                {
                    // LLAMAMOS A LA FUNCION DE TOKEN
                    GetToken();

                    // DECLARAMOS LAS FUNCIONES NECESARIAS PARA ACCEDER AL SERVICIO ON LINE
                    HttpClient httpClient = new HttpClient();
                    MultipartFormDataContent form = new MultipartFormDataContent();
                    HttpResponseMessage response;

                    // EN Recivedtmp VAMOS A GUARDAR LA RESPUESTA QUE LLEGUE DESDE EL SERVIDOR
                    String Recivedtmp = "";

                    // DEFINIMOS LAS VARIABLES DE LA FUNCION
                    httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token); // token: Excepto en la funcion "gettoken", en todas las demas deberemos enviar el token generado justamente por "gettoken" ya que es el usuario y la clave que nos permite acceder al servicio, sin esto recibiremos un mensaje de usuario incorrecto
                    form.Add(new ByteArrayContent(imageToByteArray(imageIn), 0, imageToByteArray(imageIn).Length), "photo", "image"); // photo: Envia el bytestream de la imagen la cual debe estar en formato JPG.

                    // ENVIO LOS DATOS AL SERVIDOR
                    response = await httpClient.PostAsync(ServerAPIURL + EndPointURL, form);

                    response.EnsureSuccessStatusCode();
                    httpClient.Dispose();

                    // RECIBIMOS LA RESPUESTA DESDE EL SERVIDOR
                    Recivedtmp = response.Content.ReadAsStringAsync().Result;


                    // EL STRING NOS DEVOLVERA UN UUID COMO IDENTIFICACION DEL OBJETO O ROSTRO DETECTADO POR LO CUAL DEBEREMOS REMPLAZARLO POR LA INFORMACION CANONICA QUE DECLARAMOS EN EL ADMINISTRADOR DE LA API
                    if (Recivedtmp.Trim() != "")
                    {
                        // LO PRIMERO QUE HACEMOS ES SEPARAR LA RESPUESTA ES LINEAS YA QUE outformat LO DEFINIMOS COMO pipe, PUEDEN HABER MAS DE UNA RESPUESTA SI ES QUE EN LA IMAGEN HAY MAS DE UN OBJETO DETECTABLE (EJEMPLO: HAY DOS ROSTROS)
                        String[] Metadata = Recivedtmp.Split('\n');

                        // EVALUAMOS UNA A UNA LAS RESPUESTAS
                        foreach (String Metaline in Metadata)
                        {
                            // SI LA LINEA NO ESTA VACIA IMPLICA QUE HAY UNA RESPUESTA POR PARTE DEL SERVIDOR
                            if (Metaline.Trim() != "")
                            {
                                // SEPARAMOS LAS LINEAS EN "|" YA QUE outformat LO DEFINIMOS COMO pipe
                                String[] Values = Metaline.Split('|');


                                WebClient webClient = new WebClient();
                                webClient.Headers.Add("Authorization", "Bearer " + token);

                                String response1 = webClient.DownloadString(ServerAPIURL + "/api/v2/admin/accounts/users/profiles/detections=" + Values[6] + "/value");

                                String[] RecivedMatrix1 = response1.Split('|');

                                // SI EL CODIGO RECIBIDO EN LA POSICION O ES 200 SIGNIFICA QUE EL SERVIDOR RESPONDIO CORRECTAMENTE CON EL CANONICO DEL UUID POR LO CUAL PROCEDEMOS A REMPLAZARLO EN EL STRING DE DETECCION
                                if (RecivedMatrix1[0] == "200")
                                    Recivedtmp = Recivedtmp.Replace(Values[6], RecivedMatrix1[1]);

                                // YA CON TODO RECIBIDO Y FORMATEADO PASAMOS A INTERPRETAR LA INFORMACION.
                                String[] ReceiveOnMatrix = Recivedtmp.Split('|');

                                // COMO TENEMOS DEFINIDA LA RESPUESTA CON outformat EN pipe, CADA DETECCION SERA UN STREAM QUE OCUPARA UNA LINEA DONDE CADA RESPUESTA OCUPA UNA POSICION SEPARADA PO "|"

                                // EJEMPLO DEL STREAM EN FORMATO PIPE:

                                // [CODIGO DE ACCION]|[CANONICO DEL CODIGO]|[POSICION YMIN DEL BOX DE DETECCION]|[POSICION XMAX DEL BOX DE DETECCION]|[POSICION YMAX DEL BOX DE DETECCION]|[POSICION XMAX DEL BOX DE DETECCION]|[NOMBRE O UUID DE LO DETECTADO]|[UUID DEL GRUPO IRIS]|[CONFIDENCE DE LA DETECCION O SEA QUE TAN EXACTA ES LA DETECCION]

                                // EJEMPLO PRACTICO: 

                                //  200|ok|0.0|0.44375|0.47291666666666665|0.7140625|Juan Perez|1cc9a3d4461011ea9ca300155d016a1c|0.4329930749381952\n


                                if (ReceiveOnMatrix[0] == "200")
                                {
                                    // LOS VALORES X e Y ESTAN SIN ESCALAR ESTO SIGNIFICA QUE DEBEREMOS MULTIPLICAR CADA VALOR POR EL ANCHO Y EL ALTO DE LA IMAGEN PARA OBTENER LAS COORDENADAS X/Y SOBRE LA IMAGEN 

                                    Double ymin = 0;
                                    Double xmin = 0;
                                    Double ymax = 0;
                                    Double xmax = 0;

                                    if (Convert.ToDouble(Values[2]) > 1)
                                    {
                                        ymin = Convert.ToDouble(Values[2].Replace(".", ","));
                                        xmin = Convert.ToDouble(Values[3].Replace(".", ","));
                                        ymax = Convert.ToDouble(Values[4].Replace(".", ","));
                                        xmax = Convert.ToDouble(Values[5].Replace(".", ","));
                                    }
                                    else
                                    {
                                        ymin = Convert.ToDouble(Values[2]);
                                        xmin = Convert.ToDouble(Values[3]);
                                        ymax = Convert.ToDouble(Values[4]);
                                        xmax = Convert.ToDouble(Values[5]);
                                    }

                                    Int32 left = Convert.ToInt32(xmin * 640);
                                    Int32 right = Convert.ToInt32(xmax * 640);
                                    Int32 top = Convert.ToInt32(ymin * 480);
                                    Int32 bottom = Convert.ToInt32(ymax * 480);

                                    // POR ULTIMO SEPRAMOS LOS DATOS Y LOS PRESENTAMOS
                                    String Name = ReceiveOnMatrix[6];
                                    Double Confidence = Convert.ToDouble(ReceiveOnMatrix[8]);

                                    this.Recive.Text = "Top: " + top.ToString() + " | Left: " + left.ToString() + " | Bottom: " + bottom.ToString() + " | Right: " + right.ToString() + " | Name: " + Name + " | Confidence: " + Confidence.ToString();
                                }
                            }
                        }
                    }

                    Ready = true;
                }
            }
            catch (Exception ex)
            {
                this.Recive.Text = ex.Message;
                Ready = true;
            }
        }

        public MainPage()
        {
            InitializeComponent();

            // LLAMAMOS A LA FUNCION DE TOKEN
            GetToken();
        }

        private async void TomarFoto(object sender, EventArgs e)
        {
            await CrossMedia.Current.Initialize();

            if (!CrossMedia.Current.IsTakePhotoSupported || !CrossMedia.Current.IsCameraAvailable)
            {
                await DisplayAlert("Ops", "Camara no detectada", "OK");

                return;
            }

            var file = await CrossMedia.Current.TakePhotoAsync(
                new StoreCameraMediaOptions
                {
                    SaveToAlbum = true,
                    Directory = "Horus"
                });

            if (file == null)
                return;

            this.Preview.Source = ImageSource.FromStream(() =>
            {
                var stream = file.GetStream();

                return stream;
            });

            // LEEMOS LA IMAGEN, LA REESCALAMOS Y LA SUBIMOS A LA API
            Android.Graphics.Bitmap Imagen = scaleDown(BitmapFactory.DecodeFile(file.AlbumPath), 800, false);

            Upload(Imagen);

            file.Dispose();
        }
    }
}
