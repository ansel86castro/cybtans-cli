using Cybtans.Services.Security;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Cybtans.Proto.Generator.Licencing
{
    public class LicenseValidationException:Exception
    {
        public LicenseValidationException(string message) : base(message) { }
    }

    public class LicenseService
    {
        private const string Password = "fRX5QAZXS6tF9cUtnC4umSjX3EgrcD";
        private const string PublicKey = "<RSAKeyValue><Modulus>r6gKCMfR1bxBjfI8aMNloM+DLWA6k41I+6A8LxHz4Kx7oTh4MSIHR/5CiRoUW83KhBC9X9a9yEMqnmLfjkwbRQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        private const int TrialDays = 90;

        private AppConfig LoadConfig()
        {
            var file = GetConfigFile();
            if (!File.Exists(file.FullName))
            {
                return null;
            }

            try
            {
                var configString = File.ReadAllText(file.FullName);
                SymetricCryptoService cipher = new SymetricCryptoService();
                var json = cipher.DecryptString(configString, Password);


                AppConfig config = JsonConvert.DeserializeObject<AppConfig>(json);
                return config.Id == "4E1C28A6-39E5-4092-A5E3-4B0035A882DD" ? config : null;
            }
            catch
            {
                return null;
            }
        }

        private void SaveConfig(AppConfig config)
        {           
            var json = JsonConvert.SerializeObject(config);
            SymetricCryptoService cipher = new SymetricCryptoService();
            var encripted = cipher.EncryptString(json, Password);

            var file = GetConfigFile();
            File.WriteAllText(file.FullName, encripted);
        }

        private FileInfo GetConfigFile()
        {
            var dir = Environment.CurrentDirectory;                        

            var file = Path.Combine(dir, "config.dat");
            return new FileInfo( file);
        }

        private LicenseModel LoadLicense(AppConfig config)
        {
            var file = config.LicenseFilename;
            if (file == null)
            {
                var dir = Environment.CurrentDirectory;
                file = Path.Combine(dir, "license.lic");
            }

            if (!File.Exists(file))
            {
                return null;
            }

            var json = File.ReadAllText(file);
            
            try
            {
                return JsonConvert.DeserializeObject<LicenseModel>(json);
            }
            catch
            {
                return null;
            }
        }

        private bool IsLicenseValid(LicenseModel model)
        {
            try
            {
                // Create a new instance of RSACryptoServiceProvider using the
                // key from RSAParameters.
                RSACryptoServiceProvider RSAalg = new RSACryptoServiceProvider();

                RSAalg.FromXmlString(PublicKey);

                var bytes = Encoding.UTF8.GetBytes($"{model.Id}:{model.CreateAt}:{model.Duration}");

                // Verify the data using the signature.  Pass a new instance of SHA256
                // to specify the hashing algorithm.
                return RSAalg.VerifyData(bytes, SHA256.Create(), Convert.FromBase64String(model.Signature));
            }
            catch (CryptographicException)
            {               
                return false;
            }
        }

        private bool SignLicense(LicenseModel model, string privateKey)
        {
            try
            {
                // Create a new instance of RSACryptoServiceProvider using the
                // key from RSAParameters.
                RSACryptoServiceProvider RSAalg = new RSACryptoServiceProvider();

                RSAalg.FromXmlString(privateKey);

                var bytes = Encoding.UTF8.GetBytes($"{model.Id}:{model.CreateAt}:{model.Duration}");

                // Verify the data using the signature.  Pass a new instance of SHA256
                // to specify the hashing algorithm.
                var signatureBytes = RSAalg.SignData(bytes, SHA256.Create());
                model.Signature = Convert.ToBase64String(signatureBytes);
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        public void ValidateTime()
        {
            var config = LoadConfig();
            if (config == null)
            {
                throw new LicenseValidationException(".config file not found or is corrupted");
            }

            if(config.LastExecutionTime == null)
            {
                config.LastExecutionTime = DateTime.Now;
            }
            else if(config.LastExecutionTime > DateTime.Now)
            {
                throw new LicenseValidationException("Date not valid");
            }

            if(config.StartExecutionTime == null)
            {
                config.StartExecutionTime = DateTime.Now;                
            }            

            var license = LoadLicense(config);
            if (license == null)
            {
                if(config.StartExecutionTime.Value.AddDays(TrialDays) < DateTime.Now)
                {
                    throw new LicenseValidationException("Free Trial Expired");
                }

                SaveConfig(config);
                return;
            }

            if (!IsLicenseValid(license))
                throw new LicenseValidationException("License not valid");

            var createAt = DateTime.ParseExact(license.CreateAt, "yyyy-MM-dd", null);
            if(createAt.AddDays(license.Duration) < DateTime.Now)
            {
                throw new LicenseValidationException("License expired");
            }

            SaveConfig(config);
        }
    
        public static void Verify()
        {
            LicenseService licenceManager = new LicenseService();
            licenceManager.ValidateTime();
        }

        public void EncriptFile(string srcFilename, string destFilename)
        {
            SymetricCryptoService cipher = new SymetricCryptoService();
            var content = File.ReadAllText(srcFilename);
            var encripted = cipher.EncryptString(content, Password);

            File.WriteAllText(destFilename, encripted);
        }
    
        public string CreateLicense(DateTime date, int duration, string privateKey)
        {
            var license = new LicenseModel
            {
                CreateAt = date.ToString("yyyy-MM-dd"),
                Duration = duration,
                Id = Guid.NewGuid().ToString()
            };

            if (!SignLicense(license, privateKey))
            {
                throw new InvalidOperationException("Unable to sign license");
            }

            return JsonConvert.SerializeObject(license);
        }
    }
}
