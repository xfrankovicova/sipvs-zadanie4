﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Linq;
using System.Xml.Xsl;
using System.IO;
using System.Xml.XPath;
using System.Xml.Serialization;
using System.Security;
using System.Security.Cryptography.Xml;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Tsp;
using System.Security.Cryptography;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto.Tls;
using Org.BouncyCastle.Asn1.X509;
using HashAlgorithm = System.Security.Cryptography.HashAlgorithm;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;
using System.Collections;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.X509.Store;
using Org.BouncyCastle.Asn1.Oiw;
using Org.BouncyCastle.Asn1.Nist;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Security.Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SIPVS.Controllers
{
    public class HomeController : Controller
    {
        string resultFile = @"C:\Users\ifran\Documents\sipvs-zadanie4\result-message.txt";

        public ActionResult Index()
        {
            if (System.IO.File.Exists(resultFile))
            {
                ViewBag.result = System.IO.File.ReadAllText(resultFile);
            }

            return View();
        }

        [HttpPost, ValidateInput(false)]
        public ActionResult Index(String input, String name)
        {
            try
            {
                #region Overenie datovej obalky
                XDocument doc = XDocument.Parse(input);

                if (doc.Root.Attribute(XNamespace.Xmlns + "xzep").Value.Equals("http://www.ditec.sk/ep/signature_formats/xades_zep/v1.0") == false)
                {
                    throw new Exception("Chyba v koreňovom elemente, atribút xmlns:xzep neobsahuje hodnotu http://www.ditec.sk/ep/signature_formats/xades_zep/v1.0");
                }
                else if (doc.Root.Attribute(XNamespace.Xmlns + "ds").Value.Equals("http://www.w3.org/2000/09/xmldsig#") == false)
                {
                    throw new Exception("Chyba v koreňovom elemente, atribút xmlns:ds neobsahuje hodnotu http://www.w3.org/2000/09/xmldsig#");
                }
                #endregion


                #region Kontrola obsahu ds:SignatureMethod

                XmlReader reader = XmlReader.Create(new StringReader(input));
                XElement root = XElement.Load(reader);
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(input);
                XmlNameTable nameTable = reader.NameTable;
                XmlNamespaceManager namespaceManager = new XmlNamespaceManager(nameTable);

                namespaceManager.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
                namespaceManager.AddNamespace("xzep", "http://www.ditec.sk/ep/signature_formats/xades_zep/v1.0");
                namespaceManager.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");

                XElement sigMethod = root.XPathSelectElement("//ds:Signature/ds:SignedInfo/ds:SignatureMethod", namespaceManager);

                if (sigMethod == null)
                {
                    throw new Exception("Chyba počas kontroly ds:Signature/ds:SignedInfo/ds:SignatureMethod. Element sa v dokumente nenašiel.");
                }

                string[] sigMethods = { "http://www.w3.org/2000/09/xmldsig#dsa-sha1",
                                        "http://www.w3.org/2000/09/xmldsig#rsa-sha1",
                                        "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256",
                                        "http://www.w3.org/2001/04/xmldsig-more#rsa-sha384",
                                        "http://www.w3.org/2001/04/xmldsig-more#rsa-sha512"};

                if (Array.Exists(sigMethods, element => element == sigMethod.Attribute("Algorithm").Value) == false)
                {
                    throw new Exception("Chyba v obsahu ds:SignatureMethod, atribút Algorithm neobsahuje URI niektorého z podporovaných algoritmov.");
                }

                #endregion

                #region Kontrola obsahu ds:CanonicalizationMethod

                XElement canonMethod = root.XPathSelectElement("//ds:Signature/ds:SignedInfo/ds:CanonicalizationMethod", namespaceManager);

                if (canonMethod == null)
                {
                    throw new Exception("Chyba počas kontroly ds:Signature/ds:SignedInfo/ds:CanonicalizationMethod. Element sa v dokumente nenašiel.");
                }

                string[] canonMethods = { "http://www.w3.org/TR/2001/REC-xml-c14n-20010315" };

                if (Array.Exists(canonMethods, element => element == canonMethod.Attribute("Algorithm").Value) == false)
                {
                    throw new Exception("Chyba v elemente ds:CanonicalizationMethod, Atribút Algorithm neobsahuje URI niektorého z podporovaných algoritmov");
                }

                #endregion

                #region Kontrola obsahu ds:Transforms vo vsetkych referenciach v ds:SignedInfo

                IEnumerable<XElement> transformsElems = root.XPathSelectElements("//ds:Signature/ds:SignedInfo/ds:Reference/ds:Transforms", namespaceManager);

                if (transformsElems == null)
                {
                    throw new Exception("Chyba počas kontroly ds:Signature/ds:SignedInfo/ds:Reference/ds:Transforms. Element sa v dokumente nenašiel.");
                }

                string[] transformMethods = { "http://www.w3.org/TR/2001/REC-xml-c14n-20010315" };

                foreach (XElement el in transformsElems)
                {
                    XmlElement elem = (XmlElement)xmlDoc.ReadNode(el.CreateReader());
                    XmlElement transformElement = (XmlElement)elem.GetElementsByTagName("ds:Transform").Item(0);

                    // Kontrola obsahu ds:Transforms. Musi obsahovať URI niektorého z podporovaných algoritmov

                    if (Array.Exists(transformMethods, element => element == transformElement.GetAttribute("Algorithm")) == false)
                    {
                        throw new Exception("Chyba v elemente ds:Transforms, atribút Algorithm neobsahuje URI niektorého z podporovaných algoritmov");
                    }
                }

                #endregion

                #region Kontrola obsahu ds:DigestMethod vo vsetkych referenciach v ds:SignedInfo

                IEnumerable<XElement> digestElems = root.XPathSelectElements("//ds:Signature/ds:SignedInfo/ds:Reference/ds:DigestMethod", namespaceManager);

                if (digestElems == null)
                {
                    throw new Exception("Chyba počas kontroly ds:Signature/ds:SignedInfo/ds:Reference/ds:DigestMethod. Element sa v dokumente nenašiel.");
                }

                string[] digestMethods = {  "http://www.w3.org/2000/09/xmldsig#sha1",
                                            "http://www.w3.org/2001/04/xmldsig-more#sha224",
                                            "http://www.w3.org/2001/04/xmlenc#sha256",
                                            "http://www.w3.org/2001/04/xmldsig-more#sha384",
                                            "http://www.w3.org/2001/04/xmlenc#sha512"};

                foreach (XElement el in digestElems)
                {
                    //System.Diagnostics.Debug.WriteLine(el.Attribute("Algorithm").Value);

                    if (Array.Exists(digestMethods, element => element == el.Attribute("Algorithm").Value) == false)
                    {
                        throw new Exception("Chyba v obsahu ds:DigestMethod, atribút Algorithm neobsahuje URI niektorého z podporovaných algoritmov");
                    }
                }

                #endregion


                #region Core validation - Dereferencovanie URI, kanonikalizácia referencovaných ds:Manifest elementov a overenie hodnôt odtlačkov ds:DigestValue

                Dictionary<string, string> digestAlgo = new Dictionary<string, string>() {
                    { "http://www.w3.org/2000/09/xmldsig#sha1", "SHA-1" },
                    { "http://www.w3.org/2001/04/xmldsig-more#sha224", "SHA-224" },
                    { "http://www.w3.org/2001/04/xmlenc#sha256", "SHA-256" },
                    { "http://www.w3.org/2001/04/xmldsig-more#sha384", "SHA-384" },
                    { "http://www.w3.org/2001/04/xmlenc#sha512", "SHA-512" }
                };

                IEnumerable<XElement> refElems = root.XPathSelectElements("//ds:Signature/ds:SignedInfo/ds:Reference", namespaceManager);

                if (refElems == null)
                {
                    throw new Exception("Chyba počas získavania ds:Signature/ds:SignedInfo/ds:Reference. Element sa v dokumente nenašiel.");
                }

                foreach (XElement el in refElems)
                {
                    XmlElement refElement = (XmlElement)xmlDoc.ReadNode(el.CreateReader());
                    string uri = refElement.GetAttribute("URI").Substring(1);

                    XmlElement manifestElement = FindByAttributeValue("ds:Manifest", "Id", uri, xmlDoc);
                    //System.Diagnostics.Debug.WriteLine(manifestElement);

                    if (manifestElement == null)
                    {
                        continue;
                    }

                    XmlElement digestValueElement = (XmlElement)refElement.GetElementsByTagName("ds:DigestValue").Item(0);
                    string expectedDigestValue = digestValueElement.InnerText;
                    XmlElement digestMethodElement = (XmlElement)refElement.GetElementsByTagName("ds:DigestMethod").Item(0);

                    if (Array.Exists(digestMethods, element => element == digestMethodElement.GetAttribute("Algorithm")) == false)
                    {

                        throw new Exception("Chyba v elemente ds:DigestMethod, atribút Algorithm (" + digestMethodElement.GetAttribute("Algorithm") + ") neobsahuje URI niektorého z podporovaných algoritmov");
                    }

                    string digestMethod = digestMethodElement.GetAttribute("Algorithm");

                    //System.Diagnostics.Debug.WriteLine(digestMethod);
                    digestMethod = digestAlgo[digestMethod];
                    //System.Diagnostics.Debug.WriteLine(digestMethod);

                    byte[] manifestElementBytes = null;

                    try
                    {
                        manifestElementBytes = System.Text.Encoding.UTF8.GetBytes(manifestElement.OuterXml);
                    }
                    catch (Exception e)
                    {

                        throw new Exception("Chyba pri core validacii. Transformácia z elementu na String zlyhala", e);
                    }

                    XmlNodeList transformsElements = manifestElement.GetElementsByTagName("ds:Transforms");

                    foreach (XmlElement transformsElement in transformsElements)
                    {
                        XmlElement transformElement = (XmlElement)transformsElement.GetElementsByTagName("ds:Transform").Item(0);
                        string transformMethod = transformElement.GetAttribute("Algorithm");

                        if (transformMethod.Equals("http://www.w3.org/TR/2001/REC-xml-c14n-20010315"))
                        {
                            try
                            {
                                XmlDsigC14NTransform xmlTransform = new XmlDsigC14NTransform();
                                xmlTransform.LoadInput(new MemoryStream(manifestElementBytes));
                                MemoryStream stream = (MemoryStream)xmlTransform.GetOutput();
                                manifestElementBytes = stream.ToArray();
                            }
                            catch (Exception e)
                            {

                                throw new Exception("Chyba pri kanonikalizacii", e);
                            }
                        }
                    }

                    HashAlgorithm hashAlgo = null;
                    switch (digestMethod)
                    {
                        case "SHA-1":
                            hashAlgo = SHA1.Create();
                            break;
                        case "SHA-256":
                            hashAlgo = SHA256.Create();
                            break;
                        case "SHA-384":
                            hashAlgo = SHA384.Create();
                            break;
                        case "SHA-512":
                            hashAlgo = SHA512.Create();
                            break;
                        default:
                            throw new Exception("Core validation zlyhala, neznamy algoritmus " + digestMethod);
                    }
                    string actualDigestValue = Convert.ToBase64String(hashAlgo.ComputeHash(manifestElementBytes));

                    if (expectedDigestValue.Equals(actualDigestValue) == false)
                    {
                        throw new Exception("Chyba pri overení referencii v ds:Manifest. Hash hodnota ds:Manifest sa nezhoduje s ds:DigestValue elementu ds:Reference");
                    }
                }

                #endregion


                #region Kontrola elementu ds:Signature

                XmlElement signatureElement = (XmlElement)xmlDoc.GetElementsByTagName("ds:Signature").Item(0);

                if (signatureElement == null)
                {
                    throw new Exception("Chyba, Element ds:Signature sa nenašiel");
                }

                if (signatureElement.HasAttribute("Id") == false)
                {
                    throw new Exception("Chyba, ds:Signature nemá atribút Id");
                }

                if (signatureElement.GetAttribute("Id") == "" || signatureElement.GetAttribute("Id") == null)
                {
                    throw new Exception("Chyba v elemente ds:Signature, atribút Id neobsahuje žiadnu hodnotu");
                }

                if (signatureElement.GetAttribute("xmlns:ds").Equals("http://www.w3.org/2000/09/xmldsig#") == false)
                {
                    throw new Exception("Chyba, element ds:Signature nemá nastavený namespace xmlns:ds");
                }

                #endregion

                #region Kontrola elementu ds:SignatureValue

                XmlElement signatureValueElement = (XmlElement)xmlDoc.GetElementsByTagName("ds:SignatureValue").Item(0);

                if (signatureValueElement == null)
                {
                    throw new Exception("Chyba, element ds:SignatureValue sa nenašiel.");
                }

                if (signatureValueElement.HasAttribute("Id") == false)
                {
                    throw new Exception("Chyba, ds:SignatureValue nemá atribút Id");
                }

                #endregion

                #region Overenie existencie referencií v ds:SignedInfo a hodnôt atribútov Id a Type

                IEnumerable<XElement> referencesElements = root.XPathSelectElements("//ds:Signature/ds:SignedInfo/ds:Reference", namespaceManager);

                if (referencesElements == null)
                {
                    throw new Exception("Chyba počas získavania elementu ds:Signature/ds:SignedInfo/ds:Reference. Element sa v dokumente nepodarilo nájsť.");
                }

                foreach (XElement el in referencesElements)
                {
                    XmlElement referenceElement = (XmlElement)xmlDoc.ReadNode(el.CreateReader());
                    string uri = referenceElement.GetAttribute("URI").Substring(1);
                    string actualType = referenceElement.GetAttribute("Type");

                    XElement referencedElement = null;
                    try
                    {
                        referencedElement = (XElement)root.XPathSelectElement(String.Format("//ds:Signature//*[@Id='{0}']", uri), namespaceManager);
                    }
                    catch (XPathException)
                    {
                        throw new Exception("Chyba pri overení existencie referencií v ds:SignedInfo. Chyba pri ziskavani elementu s Id: " + uri);
                    }

                    if (referencedElement == null)
                    {
                        throw new Exception("Chyba pri overení referencií v ds:SignedInfo. Nenašiel sa element s Id: " + uri);
                    }

                    XmlElement helper = (XmlElement)xmlDoc.ReadNode(referencedElement.CreateReader());
                    string referencedElementName = helper.Name;

                    Dictionary<string, string> references = new Dictionary<string, string>() {
                        { "ds:KeyInfo", "http://www.w3.org/2000/09/xmldsig#Object" },
                        { "ds:SignatureProperties", "http://www.w3.org/2000/09/xmldsig#SignatureProperties" },
                        { "xades:SignedProperties", "http://uri.etsi.org/01903#SignedProperties" },
                        { "ds:Manifest", "http://www.w3.org/2000/09/xmldsig#Manifest" },
                    };

                    if (references.ContainsKey(referencedElementName) == false)
                    {
                        throw new Exception("Chyba pri overovaní existencie referencií v ds:SignedInfo. Neznáma referencia " + referencedElementName);
                    }

                    string expectedReferenceType = references[referencedElementName];

                    if (actualType.Equals(expectedReferenceType) == false)
                    {
                        throw new Exception("Chyba pri overeni zhody referencií v ds:SignedInfo. " + expectedReferenceType + " sa nezhoduje s " + actualType);
                    }

                    XElement keyInfoReferenceElement = null;
                    try
                    {
                        keyInfoReferenceElement = (XElement)root.XPathSelectElement("//ds:Signature/ds:SignedInfo/ds:Reference[@Type='http://www.w3.org/2000/09/xmldsig#Object']", namespaceManager);
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Chyba pri overovaní existencie referencií v ds:SignedInfo." + "Chyba počas získavania elementu s Type http://www.w3.org/2000/09/xmldsig#Object", e);
                    }

                    if (keyInfoReferenceElement == null)
                    {
                        throw new Exception("Chyba v elemente ds:Reference, neexistuje referencia na ds:KeyInfo element.");
                    }

                    XElement signaturePropertieReferenceElement = null;
                    try
                    {
                        signaturePropertieReferenceElement = (XElement)root.XPathSelectElement("//ds:Signature/ds:SignedInfo/ds:Reference[@Type='http://www.w3.org/2000/09/xmldsig#SignatureProperties']", namespaceManager);
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Chyba pri overovaní existencie referencií v ds:SignedInfo." + "Chyba počas získavania elementu s Type http://www.w3.org/2000/09/xmldsig#SignatureProperties", e);
                    }

                    if (signaturePropertieReferenceElement == null)
                    {
                        throw new Exception("Chyba, v elemente ds:Reference neexistuje referencia na ds:SignatureProperties.");
                    }

                    XElement signedInfoReferenceElement = null;
                    try
                    {
                        signedInfoReferenceElement = (XElement)root.XPathSelectElement("//ds:Signature/ds:SignedInfo/ds:Reference[@Type='http://uri.etsi.org/01903#SignedProperties']", namespaceManager);
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Chyba pri overovaní existencie referencií v ds:SignedInfo." + "Chyba počas získavania elementu s Type http://uri.etsi.org/01903#SignedProperties", e);
                    }

                    if (signedInfoReferenceElement == null)
                    {
                        throw new Exception("Chyba, neexistuje referencia na xades:SignedProperties v elemente ds:Reference");
                    }
                }

                #endregion

                #region Overenie obsahu ds:KeyInfo

                XmlElement keyInfoElement = (XmlElement)xmlDoc.GetElementsByTagName("ds:KeyInfo").Item(0);

                if (keyInfoElement == null)
                {
                    throw new Exception("Chyba, element ds:KeyInfo sa nenašiel");
                }

                if (keyInfoElement.HasAttribute("Id") == false)
                {
                    throw new Exception("Chyba v elemente ds:KeyInfo, nemá atribút Id");
                }

                if (keyInfoElement.GetAttribute("Id") == "" || keyInfoElement.GetAttribute("Id") == null)
                {
                    throw new Exception("Chyba v elemente ds:KeyInfo, atribút Id neobsahuje žiadnu hodnotu");
                }

                XmlElement xDataElement = (XmlElement)keyInfoElement.GetElementsByTagName("ds:X509Data").Item(0);

                if (xDataElement == null)
                {
                    throw new Exception("Chyba v elemente ds:KeyInfo, neobsahuje element ds:X509Data");
                }

                XmlElement xCertificateElement = (XmlElement)xDataElement.GetElementsByTagName("ds:X509Certificate").Item(0);
                XmlElement xIssuerSerialElement = (XmlElement)xDataElement.GetElementsByTagName("ds:X509IssuerSerial").Item(0);
                XmlElement xSubjectNameElement = (XmlElement)xDataElement.GetElementsByTagName("ds:X509SubjectName").Item(0);

                if (xCertificateElement == null)
                {
                    throw new Exception("Chyba v elemente ds:X509Data, neobsahuje element ds:X509Certificate");
                }

                if (xIssuerSerialElement == null)
                {
                    throw new Exception("Chyba v elemente ds:X509Data, neobsahuje element ds:X509IssuerSerial");
                }

                if (xSubjectNameElement == null)
                {
                    throw new Exception("Chyba v elemente ds:X509Data, neobsahuje element ds:X509SubjectName");
                }

                XmlElement xIssuerNameElement = (XmlElement)xIssuerSerialElement.GetElementsByTagName("ds:X509IssuerName").Item(0);
                XmlElement xSerialNumberElement = (XmlElement)xIssuerSerialElement.GetElementsByTagName("ds:X509SerialNumber").Item(0);

                if (xIssuerNameElement == null)
                {
                    throw new Exception("Chyba v elemente ds:X509IssuerSerial, neobsahuje element ds:X509IssuerName");
                }

                if (xSerialNumberElement == null)
                {
                    throw new Exception("Chyba v elemente ds:X509IssuerSerial, neobsahuje element ds:X509SerialNumber");
                }

                X509Certificate certificateKeyInfo = null;
                try
                {
                    certificateKeyInfo = GetCertificate(xmlDoc);
                }
                catch (Exception e)
                {
                    throw new Exception("Chyba, certifikát X509 sa v dokumente nenašiel.", e);
                }

                System.Security.Cryptography.X509Certificates.X509Certificate cert = new System.Security.Cryptography.X509Certificates.X509Certificate(certificateKeyInfo.GetEncoded());
                String certifIssuerName = cert.Issuer;
                String certifSerialNumber = certificateKeyInfo.SerialNumber.ToString();
                String certifSubjectName = cert.Subject;

                //System.Diagnostics.Debug.WriteLine(certifIssuerName);

                if (xIssuerNameElement.FirstChild.Value.Equals(certifIssuerName) == false)
                {
                    throw new Exception("Chyba, element ds:X509IssuerName sa nezhoduje s hodnotou na certifikáte");
                }

                if (xSerialNumberElement.FirstChild.Value.Equals(certifSerialNumber) == false)
                {
                    throw new Exception("Chyba, element ds:X509SerialNumber sa nezhoduje s hodnotou na certifikáte");
                }

                if (xSubjectNameElement.FirstChild.Value.Equals(certifSubjectName) == false)
                {
                    throw new Exception("Chyba v element ds:X509SubjectName, neobsahuje element ds:X509SerialNumber");
                }

                #endregion

                #region Overenie obsahu ds:SignatureProperties

                XmlElement signaturePropertiesElement = (XmlElement)xmlDoc.GetElementsByTagName("ds:SignatureProperties").Item(0);

                if (signaturePropertiesElement == null)
                {
                    throw new Exception("Chyba, element ds:SignatureProperties sa nepodarilo nájsť.");
                }

                if (signaturePropertiesElement.HasAttribute("Id") == false)
                {
                    throw new Exception("Chyba v elemente ds:SignatureProperties, nemá atribút Id.");
                }

                if (signaturePropertiesElement.GetAttribute("Id") == "" || signaturePropertiesElement.GetAttribute("Id") == null)
                {
                    throw new Exception("Chyba v elemente ds:SignatureProperties, atribút Id neobsahuje žiadnu hodnotu");
                }

                XmlElement signatureVersionElement = null;
                XmlElement productInfosElement = null;

                XmlNodeList sigPropsElement = signaturePropertiesElement.GetElementsByTagName("ds:SignatureProperty");

                foreach (XmlElement tempElement in sigPropsElement)
                {

                    if (tempElement != null)
                    {

                        XmlElement tempElement2 = (XmlElement)tempElement.GetElementsByTagName("xzep:SignatureVersion").Item(0);

                        if (tempElement2 != null)
                        {
                            signatureVersionElement = tempElement2;
                        }

                        else
                        {
                            tempElement2 = (XmlElement)tempElement.GetElementsByTagName("xzep:ProductInfos").Item(0);

                            if (tempElement != null)
                            {
                                productInfosElement = tempElement2;
                            }
                        }
                    }
                }

                if (signatureVersionElement == null)
                {
                    throw new Exception("Chyba v elemente ds:SignatureProperties, element neobsahuje taký ds:SignatureProperty, ktorý by obsahoval xzep:SignatureVersion");
                }

                if (productInfosElement == null)
                {
                    throw new Exception("Chyba v elemente ds:SignatureProperties, element neobsahuje taký ds:SignatureProperty, ktorý by obsahoval xzep:ProductInfos");
                }

                XmlElement signature = (XmlElement)xmlDoc.GetElementsByTagName("ds:Signature").Item(0);

                if (signature == null)
                {
                    throw new Exception("Chyba, element ds:Signature sa nenašiel.");
                }

                String signatureId = signature.GetAttribute("Id");
                XmlElement sigVerParentElement = (XmlElement)signatureVersionElement.ParentNode;
                XmlElement pInfoParentElement = (XmlElement)productInfosElement.ParentNode;
                String targetSigVer = sigVerParentElement.GetAttribute("Target");
                String targetPInfo = pInfoParentElement.GetAttribute("Target");

                if (targetSigVer.Equals("#" + signatureId) == false)
                {
                    throw new Exception("Chyba v elemente xzep:SignatureVersion, atribút Target neodkazuje na daný ds:Signature");
                }

                if (targetPInfo.Equals("#" + signatureId) == false)
                {
                    throw new Exception("Chyba v elemente xzep:ProductInfos, atribút Target neodkazuje na daný ds:Signature");
                }

                #endregion

                #region Overenie ds:Manifest elementov

                IEnumerable<XElement> manifestElems = root.XPathSelectElements("//ds:Signature/ds:Object/ds:Manifest", namespaceManager);

                if (manifestElems == null)
                {
                    throw new Exception("Chyba počas vyhľadávania elementov ds:Manifest.");
                }

                foreach (XElement manifestElem in manifestElems)
                {
                    //každý ds:Manifest element musí mať Id atribút

                    XmlElement manElement = (XmlElement)xmlDoc.ReadNode(manifestElem.CreateReader());

                    if (manElement.HasAttribute("Id") == false)
                    {
                        throw new Exception("Chyba, element ds:Manifest nemá atribút Id");
                    }

                    IEnumerable<XElement> referElements = manifestElem.XPathSelectElements("ds:Reference", namespaceManager);

                    if (referElements == null)
                    {
                        throw new Exception("Chyba počas vyhľadávania elementov ds:Reference elemente v ds:Manifest.");
                    }

                    //každý ds:Manifest element musí obsahovať práve jednu referenciu na ds:Object

                    if (referElements.Count() != 1)
                    {
                        throw new Exception("Chyba, element ds:Manifest neobsahuje práve 1 referenciu na objekt.");
                    }
                }

                IEnumerable<XElement> referenceElements = root.XPathSelectElements("//ds:Signature/ds:Object/ds:Manifest/ds:Reference", namespaceManager);

                if (referenceElements == null)
                {
                    throw new Exception("Chyba počas vyhľadávania elementov ds:Reference.");
                }

                foreach (XElement referenceElement in referenceElements)
                {
                    IEnumerable<XElement> transformElems = root.XPathSelectElements("ds:Transforms/ds:Transform", namespaceManager);

                    if (transformElems == null)
                    {
                        throw new Exception("Chyba počas vyhľadávania elementov ds:Transform.");
                    }

                    foreach (XElement transformElem in transformElems)
                    {
                        string[] manifestMethods = {
                                                    "http://www.w3.org/TR/2001/REC-xml-c14n-20010315",
                                                    "http://www.w3.org/2000/09/xmldsig#base64",
                                                    };
                        //ds:Transforms musí byť z množiny podporovaných algoritmov pre daný element podľa profilu XAdES_ZEP

                        if (Array.Exists(manifestMethods, element => element == transformElem.Attribute("Algorithm").Value) == false)
                        {
                            throw new Exception("Chyba, element ds:Transform obsahuje nepovolený typ algoritmu.");
                        }
                    }

                    XElement digestMethodElement = referenceElement.XPathSelectElement("ds:DigestMethod", namespaceManager);

                    if (digestMethodElement == null)
                    {
                        throw new Exception("Chyba počas vyhľadávania elementov ds:DigestMethod.");
                    }

                    //ds:DigestMethod – musí obsahovať URI niektorého z podporovaných algoritmov podľa profilu XAdES_ZEP

                    if (Array.Exists(digestMethods, element => element == digestMethodElement.Attribute("Algorithm").Value) == false)
                    {
                        throw new Exception("Chyba v elemente ds:DigestMethod, atribút Algorithm neobsahuje URI niektorého z podporovaných algoritmov");
                    }

                    //overenie hodnoty Type atribútu voči profilu XAdES_ZEP

                    if (referenceElement.Attribute("Type").Value.Equals("http://www.w3.org/2000/09/xmldsig#Object") == false)
                    {
                        throw new Exception("Chyba v elemente ds:Reference, Atribút Type neobsahuje hodnotu http://www.w3.org/2000/09/xmldsig#Object");
                    }

                }

                #endregion

                #region Overenie časovej pečiatky

                TimeStampToken ts_token = null;

                XElement timestamp = null;

                timestamp = (XElement)root.XPathSelectElement("//xades:EncapsulatedTimeStamp", namespaceManager);

                if (timestamp == null)
                {
                    throw new Exception("Chyba, v dokumente sa nenašla časová pečiatka.");
                }

                ts_token = new TimeStampToken(new CmsSignedData(Base64.Decode(timestamp.Value)));

                #region Overenie platnosti podpisového certifikátu časovej pečiatky voči času UtcNow a voči platnému poslednému CRL

                X509CrlParser crlParser = new X509CrlParser();

                //WebClient webClient = new WebClient();
                //webClient.DownloadFile("http://test.ditec.sk/TSAServer/crl/dtctsa.crl", "dtctsa.crl");
                X509Crl crl = crlParser.ReadCrl(System.IO.File.ReadAllBytes("C:\\Users\\ifran\\Documents\\sipvs-zadanie4\\crls\\dtctsa.crl"));

                X509Certificate signerCert = null;
                IX509Store x509Certs = ts_token.GetCertificates("Collection");
                ArrayList certs = new ArrayList(x509Certs.GetMatches(null));

                // nájdenie podpisového certifikátu tokenu v kolekcii
                foreach (X509Certificate certifikat in certs)
                {
                    string cerIssuerName = certifikat.IssuerDN.ToString(true, new Hashtable());
                    string signerIssuerName = ts_token.SignerID.Issuer.ToString(true, new Hashtable());

                    // kontrola issuer name a seriového čísla
                    if (cerIssuerName == signerIssuerName && certifikat.SerialNumber.Equals(ts_token.SignerID.SerialNumber))
                    {
                        signerCert = certifikat;
                        break;
                    }
                }

                if (signerCert == null)
                {
                    throw new Exception("Chyba, nenašiel sa certifikát časovej pečiatky.");
                }

                if (!signerCert.IsValidNow)
                {
                    throw new Exception("Chyba, podpisový certifikát časovej pečiatky je neplatný voči aktuálnemu času.");
                }

                if (crl.GetRevokedCertificate(signerCert.SerialNumber) != null)
                {
                    throw new Exception("Chyba, podpisový certifikát časovej pečiatky je neplatný voči platnému poslednému CRL.");
                }

                #endregion

                #region Overenie MessageImprint z časovej pečiatky voči podpisu ds:SignatureValue

                byte[] messageImprint = ts_token.TimeStampInfo.GetMessageImprintDigest();
                //System.Diagnostics.Debug.WriteLine(Convert.ToBase64String(messageImprint));
                String hashAlg = ts_token.TimeStampInfo.HashAlgorithm.Algorithm.Id;

                XElement signatureValueNode = null;

                signatureValueNode = root.XPathSelectElement("//ds:Signature/ds:SignatureValue", namespaceManager);

                if (signatureValueNode == null)
                {
                    throw new Exception("Chyba, element ds:SignatureValue sa nenašiel.");
                }

                //XmlElement sigValueNode = (XmlElement)xmlDoc.ReadNode(signatureValueNode.CreateReader());
                byte[] signatureValue = Encoding.Default.GetBytes(signatureValueNode.Value);

                Dictionary<string, string> algorithms = new Dictionary<string, string>() {
                    { OiwObjectIdentifiers.IdSha1.Id, "SHA-1"},
                    { NistObjectIdentifiers.IdSha256.Id, "SHA-256"},
                    { NistObjectIdentifiers.IdSha384.Id, "SHA-384"},
                    { NistObjectIdentifiers.IdSha512.Id, "SHA-512"},
                };

                string digMethod = algorithms[hashAlg];

                HashAlgorithm hashTAlgo = null;
                switch (digMethod)
                {
                    case "SHA-1":
                        hashTAlgo = SHA1.Create();
                        break;
                    case "SHA-256":
                        hashTAlgo = SHA256.Create();
                        break;
                    case "SHA-384":
                        hashTAlgo = SHA384.Create();
                        break;
                    case "SHA-512":
                        hashTAlgo = SHA512.Create();
                        break;
                    default:
                        throw new Exception("Chyba počas core validácie, neznámy algoritmus " + digMethod);
                }

                byte[] comparisonVal = hashTAlgo.ComputeHash(signatureValue);

                if (messageImprint.Equals(comparisonVal))
                {
                    throw new Exception("Chyba, hodnoty podpisu ds:SignatureValue a MessageImprint z časovej pečiatky sa nezhodujú.");
                }

                #endregion

                #endregion

                #region Overenie referencií v elementoch ds:Manifest

                IEnumerable<XElement> manRefElements = root.XPathSelectElements("//ds:Signature/ds:Object/ds:Manifest/ds:Reference", namespaceManager);

                if (manRefElements == null)
                {
                    throw new Exception("Chyba pri hľadaní ds:Reference elementov v dokumente");
                }

                for (int i = 0; i < manRefElements.Count(); i++)
                {
                    XElement refElem = (XElement)manRefElements.ElementAt(i);
                    XmlElement referElement = (XmlElement)xmlDoc.ReadNode(refElem.CreateReader());
                    String uri = referElement.GetAttribute("URI").Substring(1);

                    //XmlElement objectElement = findByAttributeValue("ds:Object", "Id", uri, xmlDoc);
                    XmlNode objectElement = xmlDoc.SelectSingleNode(@"//ds:Object[@Id='" + uri + "']", namespaceManager);

                    XmlElement digestValElement = (XmlElement)referElement.GetElementsByTagName("ds:DigestValue").Item(0);
                    XmlElement digestMethodlement = (XmlElement)referElement.GetElementsByTagName("ds:DigestMethod").Item(0);

                    String digestMethod = digestMethodlement.GetAttribute("Algorithm");
                    digestMethod = digestAlgo[digestMethod];

                    XmlNodeList transfsElements = referElement.GetElementsByTagName("ds:Transforms");

                    for (int j = 0; j < transfsElements.Count; j++)
                    {
                        XmlElement transfsElement = (XmlElement)transfsElements.Item(j);
                        XmlElement transfElement = (XmlElement)transfsElement.GetElementsByTagName("ds:Transform").Item(j);

                        String transfMethod = transfElement.GetAttribute("Algorithm");
                        //System.Diagnostics.Debug.WriteLine(objectElement.OuterXml);
                        string xElement = objectElement.OuterXml;
                        byte[] objectElementBytes = System.Text.Encoding.UTF8.GetBytes(xElement.Replace(System.Environment.NewLine, "\n"));

                        if (objectElementBytes == null)
                        {
                            throw new Exception("Chyba pri tranformácii elementu na String");
                        }

                        MemoryStream str = new MemoryStream(objectElementBytes);

                        if (transfMethod.Equals("http://www.w3.org/TR/2001/REC-xml-c14n-20010315"))
                        {
                            try
                            {
                                XmlDsigC14NTransform xxmlTransform = new XmlDsigC14NTransform(true);
                                xxmlTransform.LoadInput(str);
                                MemoryStream stream = (MemoryStream)xxmlTransform.GetOutput();
                                objectElementBytes = stream.ToArray();
                            }
                            catch (Exception e)
                            {
                                throw new Exception("Chyba počas kanonikalizácie", e);
                            }
                        }

                        if (transfMethod.Equals("http://www.w3.org/2000/09/xmldsig#base64"))
                        {
                            objectElementBytes = Base64.Decode(objectElementBytes);
                        }

                        HashAlgorithm messageDigest = null;
                        switch (digestMethod)
                        {
                            case "SHA-1":
                                messageDigest = SHA1.Create();
                                break;
                            case "SHA-256":
                                messageDigest = SHA256.Create();
                                break;
                            case "SHA-384":
                                messageDigest = SHA384.Create();
                                break;
                            case "SHA-512":
                                messageDigest = SHA512.Create();
                                break;
                            default:
                                throw new Exception("Chyba počas core validácie, neznámy algoritmus " + digestMethod);
                        }

                        string actDigestValue = Convert.ToBase64String(messageDigest.ComputeHash(objectElementBytes));
                        //System.Diagnostics.Debug.WriteLine(actDigestValue);
                        String expectedDigestVal = digestValElement.InnerText;
                        //System.Diagnostics.Debug.WriteLine(expectedDigestVal);

                        if (expectedDigestVal.Equals(actDigestValue) == false)
                        {
                            throw new Exception("Chyba, hash hodnota ds:Manifest sa nezhoduje s ds:DigestValue elementu ds:Reference.");
                        }
                    }
                }

                #endregion


                #region Core validation - Kanonikalizácia ds:SignedInfo a overenie hodnoty ds:SignatureValue

                signatureElement = (XmlElement)xmlDoc.GetElementsByTagName("ds:Signature").Item(0);

                XmlElement signedInfoElement = (XmlElement)signatureElement.GetElementsByTagName("ds:SignedInfo").Item(0);
                XmlElement canonicalizationMethodElement = (XmlElement)signedInfoElement.GetElementsByTagName("ds:CanonicalizationMethod").Item(0);

                Dictionary<string, string> signAlgo = new Dictionary<string, string>() {
                    { "http://www.w3.org/2000/09/xmldsig#dsa-sha1", "SHA1withDSA" },
                    { "http://www.w3.org/2000/09/xmldsig#rsa-sha1", "SHA1withRSA/ISO9796-2" },
                    { "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256", "SHA256withRSA" },
                    { "http://www.w3.org/2001/04/xmldsig-more#rsa-sha384", "SHA384withRSA" },
                    { "http://www.w3.org/2001/04/xmldsig-more#rsa-sha512", "SHA512withRSA" }
                };

                XmlElement signatureMethodElement = (XmlElement)signedInfoElement.GetElementsByTagName("ds:SignatureMethod").Item(0);
                signatureValueElement = (XmlElement)signatureElement.GetElementsByTagName("ds:SignatureValue").Item(0);

                byte[] signedInfoElementBytes = null;
                try
                {
                    signedInfoElementBytes = System.Text.Encoding.Default.GetBytes(signedInfoElement.OuterXml);
                }
                catch (Exception e)
                {
                    throw new Exception("Chyba počas tranformácie elementu na String", e);
                }

                string canonicalizationMethod = canonicalizationMethodElement.GetAttribute("Algorithm");

                if (canonicalizationMethod.Equals("http://www.w3.org/TR/2001/REC-xml-c14n-20010315"))
                {
                    try
                    {
                        XmlDsigC14NTransform xmlTransform = new XmlDsigC14NTransform();
                        xmlTransform.LoadInput(new MemoryStream(signedInfoElementBytes));
                        MemoryStream stream = (MemoryStream)xmlTransform.GetOutput();
                        signedInfoElementBytes = stream.ToArray();
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Chyba počas kanonikalizácie.", e);
                    }
                }

                X509Certificate certificate = null;

                try
                {
                    certificate = GetCertificate(xmlDoc);
                }
                catch (Exception e)
                {
                    throw new Exception("Chyba, certifikát X509 sa v dokumente nenašiel.", e);
                }

                string signatureMethod = signatureMethodElement.GetAttribute("Algorithm");
                signatureMethod = signAlgo[signatureMethod];
                ISigner signer = null;

                try
                {
                    signer = SignerUtilities.GetSigner(signatureMethod);
                    signer.Init(false, certificate.GetPublicKey());
                    signer.BlockUpdate(signedInfoElementBytes, 0, signedInfoElementBytes.Length);
                }
                catch (Exception e)
                {
                    throw new Exception("Chyba počas inicializácie digitálneho podpisovača.", e);
                }

                byte[] signatureValueBytes = System.Text.Encoding.UTF8.GetBytes(signatureValueElement.OuterXml);

                bool verificationResult = false;

                try
                {
                    verificationResult = signer.VerifySignature(signatureValueBytes);
                }
                catch (SignatureException e)
                {
                    throw new Exception("Chyba počas verifikacie digitalneho podpisu", e);
                }

                if (verificationResult == false)
                {
                    throw new Exception("Chyba, hodnota v ds:SignatureValue sa nezhoduje s hodnotou v ds:SignedInfo");
                }

                #endregion


                #region Overenie platnosti podpisovaneho certifikatu

                VerifyCertificate(root, crlParser, ts_token, namespaceManager);

                #endregion

                string resultMessage = name + ": XML súbor je validný.";
                System.IO.File.WriteAllText(resultFile, resultMessage);
                ViewBag.result = resultMessage;
                return View();
            }
            catch (Exception ex)
            {
                string resultMessage = name + ": " + ex.Message;
                System.IO.File.WriteAllText(resultFile, resultMessage);
                ViewBag.result = resultMessage;
                return View();
            }
        }

        private XmlElement FindByAttributeValue(string elType, string attributeName, string attributeValue, XmlDocument xmlDoc)
        {

            XmlNodeList elements = xmlDoc.GetElementsByTagName(elType);

            for (int i = 0; i < elements.Count; i++)
            {

                XmlElement element = (XmlElement)elements.Item(i);

                if (element.HasAttribute(attributeName) && element.GetAttribute(attributeName).Equals(attributeValue))
                {
                    return element;
                }
            }

            return null;
        }

        private X509Certificate GetCertificate(XmlDocument xmlDoc)
        {

            XmlElement keyInfoElement = (XmlElement)xmlDoc.GetElementsByTagName("ds:KeyInfo").Item(0);

            if (keyInfoElement == null)
            {
                throw new Exception("Chyba v obsahu ds:KeyInfo, dokument neobsahuje element ds:KeyInfo");
            }

            XmlElement x509DataElement = (XmlElement)keyInfoElement.GetElementsByTagName("ds:X509Data").Item(0);

            if (x509DataElement == null)
            {
                throw new Exception("Chyba v obsahu ds:KeyInfo, dokument neobsahuje element ds:X509Data");
            }

            XmlElement x509Certificate = (XmlElement)x509DataElement.GetElementsByTagName("ds:X509Certificate").Item(0);

            if (x509Certificate == null)
            {
                throw new Exception("Chyba v obsahu ds:X509Data, dokument neobsahuje element ds:X509Certificate");
            }

            X509Certificate certObject = null;
            Asn1InputStream inputStream = null;

            try
            {
                inputStream = new Asn1InputStream(new MemoryStream(Convert.FromBase64String(x509Certificate.InnerText)));
                Asn1Sequence sequence = (Asn1Sequence)inputStream.ReadObject();
                certObject = new X509Certificate(X509CertificateStructure.GetInstance(sequence));
            }
            catch (Exception e)
            {
                throw new Exception("Chyba, certifikát nie je možné načítať", e);
            }
            finally
            {
                if (inputStream != null)
                {
                    inputStream.Close();
                }
            }

            return certObject;
        }

        private bool VerifyCertificate(XElement root, X509CrlParser crlParser, TimeStampToken ts_token, XmlNamespaceManager namespaceManager)
        {
            #region Overenie platnosti podpisového certifikátu dokumentu voči času T z časovej pečiatky a voči platnému poslednému CRL

            //X509Crl crl2 = crlParser.ReadCrl(System.IO.File.ReadAllBytes(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "//DTCCACrl.crl"));
            X509Crl crl2 = crlParser.ReadCrl(System.IO.File.ReadAllBytes("C:\\Users\\ifran\\Documents\\sipvs-zadanie4\\crls\\DTCCACrl.crl"));

            XElement certificateNode = root.XPathSelectElement("//ds:Signature/ds:KeyInfo/ds:X509Data/ds:X509Certificate", namespaceManager);

            if (certificateNode == null)
            {
                throw new Exception("Chyba, element ds:X509Certificate sa nenašiel.");
            }

            X509Certificate signCert = null;
            Asn1InputStream asn1is = null;

            try
            {
                asn1is = new Asn1InputStream(new MemoryStream(Encoding.UTF8.GetBytes(certificateNode.Value)));
                //System.Diagnostics.Debug.WriteLine(certificateNode.Value);
                Asn1Sequence sq = (Asn1Sequence)asn1is.ReadObject();
                signCert = new X509Certificate(X509CertificateStructure.GetInstance(sq));
            }
            catch (Exception)
            {
                throw new Exception("Chyba, nie je možné prečítať certifikát dokumentu.");
            }
            finally
            {
                if (asn1is != null)
                {
                    asn1is.Close();
                }
            }

            try
            {
                signCert.CheckValidity(ts_token.TimeStampInfo.GenTime);
            }
            catch (CertificateExpiredException)
            {
                throw new CertificateExpiredException("Chyba, expirovaný certifikát dokumentu pri podpise.");
            }
            catch (CertificateNotYetValidException)
            {
                throw new CertificateNotYetValidException("Chyba, certifikát dokumentu neplatný v čase podpisovania.");
            }

            X509CrlEntry entry = crl2.GetRevokedCertificate(signCert.SerialNumber);
            
            if (entry != null && entry.RevocationDate < ts_token.TimeStampInfo.GenTime)
            {
                throw new Exception("Chyba, certifikát zrušený počas podpisovania.");
            }

            return true;

            #endregion
        }

    }
}