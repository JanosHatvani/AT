using NUnit.Framework;
using Modules;
using Winium;
using System;
using OpenQA.Selenium.Winium;
using System.Windows.Forms;
using OpenQA.Selenium;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using OpenQA.Selenium.Support.UI;
using System.Threading;

namespace TruMES_Test
{
    public class ReleaseTest
    {
        public static WiniumDriver driver { get; set; }
        private const string WiniumDriverDirectory = @"C:\Winiumdriver\";

        string testname = "releasetest240726";

        string folderpath = "C:\\WorkSpace\\240726\\";

        string database = "KONZ TESZT(PROXMOX)";
        string felhasznalo = "dmsdba";
        string password = "gum1cukor";

        string cikkszam = "AUTITEM356B";        /*csak nagybetûvel!!!*/
        /*csak nagybetûvel!!!*/
        string cikkmegnevezes = "AUTITEM356B";    /*csak nagybetûvel!!!*/
        string cikkcsopcode = "31";
        string vevoilistaAr = "651";
        string vevoilistaArMargin = "10";
        string partner = "AUTCOMPY356B";   /*csak nagybetûvel!!!*/
        string providnev = "AT356B";       /*csak nagybetûvel!!!*/
        string szamlazasiAddress = "Pont az út 23";
        string levelezesiAddress = "Pont Nem az utca 22";
        string partnermail = "company356b@mail.hu";  /*csak kisbetûvel!!!*/
        string taxcode = "11111111-3-48";
        string partnernote = "megjegyzés partner240131";

        string cikkszam2 = "MEGRENDCIKK15B";
        string cikkmegnevezes2 = "CMMEGRENDCIKK15B";
        string cikkcsopcode2 = "8";

        string partner2 = "AUTCBSZ7B";   /*csak nagybetûvel!!!*/
        string providnev2 = "AB7B";       /*csak nagybetûvel!!!*/
        string szamlazasiAddress2 = "Pont az út 23";
        string levelezesiAddress2 = "Pont Nem az utca 22";
        string partnermail2 = "atb7b@mail.hu";  /*csak kisbetûvel!!!*/
        string taxcode2 = "11111111-3-47";

        string projektmegnkulsocode = "releasetest240131";

        string megrendelesmennyiseg = "10";

        string megrendelesegysegar = "1011";

        string szallito = "CSERNYIK ÁKOS";
        string koltseghely = "Test";

        string szallitolevel = "szlev000029";
        string szamlaszam = "szlszám000029";
        string szallitolevel2 = "bszlev000029";
        string szamlaszam2 = "bszlszám000029";

        string today = DateTime.Today.ToString("yyyy/MM/dd.");
        string muszakrend = "délelõtt";
        string munkatárs = "GÉPKEZELÕ ZOLTÁN";
        string reszleg = "R009 - MEGMUNKÁLÓ";
        string appPath = "c:\\Program Files\\Notepad++\\notepad++.exe";

        // master - "T:\\dms\\bin\\k2dfactory\\RunK2DFactoryApplication.exe";
        // tesztverió - levélben szereplõ elérési út!

        [SetUp]
        public void Setup()
        {
        //    WDMethods.Start(appPath, WiniumDriverDirectory);
        //    WaitTime.Wait(4);
        //    LogWriterT.LogDirFileCreate(folderpath, testname);
        //    WaitTime.Wait(1);
        }

        [Test]
        public void Releasetest()
        {
            // Bejelentkezés

            //WDMethods.TextClear("Scintilla", PropertyTypes.Id);
            //WDMethods.Sendkeys("Scintilla", felhasznalo, PropertyTypes.Id);
            


            //    WDMethods.Click("SETTINGS_TAB", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "beállítások tab katt");

            //    WDMethods.Click("MAINTENANCE_ORDINARY", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "törzsek katt");

            //    WDMethods.WDSendkeys("Keresés...", "cikkek", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "cikkek beírva");

            //    WDMethods.Click("MAINTENANCE_PARTS", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikkekre katt");
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    /*profil betöltése cikkek*/

            //    WDMethods.RightClick("PART_DropDownButton", PropertyTypes.Id);
            //    WDMethods.RightClick("GridView profil betöltése", PropertyTypes.Name);
            //    WDMethods.Click("Releasetest", PropertyTypes.Name);
            //    WDMethods.Click("OkButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "profil betöltve");
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.Click("PARTS_ADD_BUTTON", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "új cikkek hozzáadás");

            //    WDMethods.Click("RadRibbonButton", PropertyTypes.ClassName);
            //    LogWriterT.WriteLog(folderpath, testname, "új cikkek katt");

            //    WDMethods.Sendkeys("CodeTextBox", cikkszam, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikkekszám beírás");

            //    WDMethods.Sendkeys("PartNational", cikkmegnevezes, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "cikkmegnevezés beírás");
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.Click("toggleButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikkcsoport választó katt");

            //    WDMethods.Sendkeys("FilterTextBoxEditor_0", cikkcsopcode, PropertyTypes.Id);
            //    WDMethods.driver.FindElement(By.Id("FilterTextBoxEditor_0")).Submit();
            //    LogWriterT.WriteLog(folderpath, testname, "cikkcsoport beírás");

            //    //WDMethods.Click("PART_DropDownButton", PropertyTypes.Id); 
            //    //LogWriterT.WriteLog(folderpath, testname, "cikkcsoport PART_DropDownButton katt");

            //    WDMethods.Click(cikkcsopcode, PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "cikkcsoport katt");
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.Click("SelectCommand", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "OK katt");

            //    WDMethods.Sendkeys("PartBarCode", cikkmegnevezes, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk vonalkód beírás");
            ;
            //    WDMethods.Sendkeys("PartExtCode", cikkmegnevezes, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk vonalkód beírás");

            //    WDMethods.Sendkeys("CustomerListPrice", vevoilistaAr, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk vevõi listaár beírás");

            //    WDMethods.Sendkeys("MarginNtb", vevoilistaArMargin, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk margin beírás");

            //    WDMethods.Sendkeys("PartUnitWeight", "21", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk nettó súly beírás");
            //    WaitTime.Wait(1);

            //    WDMethods.Sendkeys("PartBruttoWeight", "25", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk bruttó súly beírás");

            //    WDMethods.ScrollToElementAndClick("PartBruttoWeightUnitType", PropertyTypes.Id, "GR");
            //    LogWriterT.WriteLog(folderpath, testname, "bruttosúly típus katt");;

            //    WDMethods.Click("Ok", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "mentés");
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("btClose", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk törzs karbantartó ablak bezárás");

            //    /* Cikk 2 */

            //    WDMethods.Click("PARTS_ADD_BUTTON", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "új cikkek hozzáadás");

            //    WDMethods.Click("RadRibbonButton", PropertyTypes.ClassName);
            //    LogWriterT.WriteLog(folderpath, testname, "új cikkek katt");

            //    WDMethods.Sendkeys("CodeTextBox", cikkszam2, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikkekszám beírás");

            //    WDMethods.Sendkeys("PartNational", cikkmegnevezes2, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "cikkmegnevezés beírás");

            //    WDMethods.Click("toggleButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikkcsoport választó katt");

            //    WDMethods.Sendkeys("FilterTextBoxEditor_0", cikkcsopcode2, PropertyTypes.Id);
            //    WDMethods.driver.FindElement(By.Id("FilterTextBoxEditor_0")).Submit();
            //    LogWriterT.WriteLog(folderpath, testname, "cikkcsoport beírás");

            //    //WDMethods.WDClick("PART_DropDownButton", PropertyTypes.Id); 
            //    //LogWriterT.WriteLog(folderpath, testname, "cikkcsoport PART_DropDownButton katt");

            //    WDMethods.WDClick(cikkcsopcode2, PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "cikkcsoport katt");

            //    WDMethods.WDClick("SelectCommand", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "OK katt");

            //    WDMethods.WDSendkeys("CustomerListPrice", vevoilistaAr, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk vevõi listaár beírás");

            //    WDMethods.WDSendkeys("MarginNtb", vevoilistaArMargin, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk margin beírás");

            //    WDMethods.WDSendkeys("PartUnitWeight", "21", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk nettó súly beírás");

            //    WDMethods.WDSendkeys("PartBruttoWeight", "25", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk bruttó súly beírás");

            //    WDMethods.WDScrollToElementAndClick("PartBruttoWeightUnitType", PropertyTypes.Id, "GR");
            //    LogWriterT.WriteLog(folderpath, testname, "bruttosúly típus katt");

            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "mentés");
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("btClose", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk törzs karbantartó ablak bezárás");


            //    /*-------------------beállítások partnerek-------------------*/

            //    WDMethods.TextClear("Keresés...", PropertyTypes.Name);
            //    WDMethods.Sendkeys("Keresés...", "partnerek", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "partnerek beírva");

            //    WDMethods.Click("MAINTENANCE_PARTNERS", PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "partnerek katt");

            //    /*profil betöltésse partnerek*/

            //    WDMethods.RightClick("PART_HeaderRow", PropertyTypes.Id);
            //    WDMethods.RightClick("GridView profil betöltése", PropertyTypes.Name);
            //    WDMethods.Click("Releasetest", PropertyTypes.Name);
            //    WDMethods.Click("OkButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "profil betöltve");
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    /* vevõ rögzítése */

            //    WDMethods.Click("addPartner_btn", PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "hozzáadás katt");

            //    WDMethods.Sendkeys("PartnerName", partner, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "partner név beírva");

            //    WDMethods.Sendkeys("PartnerNickName", providnev, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "partner rövidnév beírva");

            //    WDMethods.Sendkeys("Keresés...", "Szolnok 5000", PropertyTypes.Name); ;
            //    LogWriterT.WriteLog(folderpath, testname, "város név beírva");
            //    SendKeys.SendWait("{Enter}");

            //    //WDMethods.Click("Szolnok 5000", PropertyTypes.Name); ;
            //    //LogWriterT.WriteLog(folderpath, testname, "város név katt");

            //    WDMethods.Sendkeys("PartnerInvAddress", szamlazasiAddress, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "város név beírva");

            //    WDMethods.Click("A számlázási cím megegyzik a levelezési címmel?", PropertyTypes.Name);
            //    WDMethods.Click("A számlázási cím megegyzik a levelezési címmel?", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "cím másolás be-kikapcsolás katt");

            //    WDMethods.TextClear("PartnerCorAddress", PropertyTypes.Id);
            //    WDMethods.Sendkeys("PartnerCorAddress", levelezesiAddress, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "levelezési cím beírva");
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.Sendkeys("PartnerEmail", partnermail, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "email beírva");

            //    WDMethods.ScrollToElementAndClick("PartnerType", PropertyTypes.Id, "Társas vállalkozás");
            //    LogWriterT.WriteLog(folderpath, testname, "partner type kiválasztás");

            //    WDMethods.ScrollToElementAndClick("PartnerCompanyForm", PropertyTypes.Id, "KFT.");
            //    LogWriterT.WriteLog(folderpath, testname, "partner form kiválasztás");

            //    WDMethods.ScrollToElementAndClick("PartnerDepartmentType", PropertyTypes.Id, "Ipar, építõipar");
            //    LogWriterT.WriteLog(folderpath, testname, "ágazat kiválasztása");

            //    WDMethods.ScrollToElementAndClick("PartnerPayClass", PropertyTypes.Id, "Jól fizetõ");
            //    LogWriterT.WriteLog(folderpath, testname, "fizetési minõsítés katt");

            //    WDMethods.ScrollToElementAndClick("DefaultCurrency", PropertyTypes.Id, "HUF");
            //    LogWriterT.WriteLog(folderpath, testname, "alapértelmezett pénznem kiv.");

            //    WDMethods.Sendkeys("PartnerTaxaccount", taxcode, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "tax beírva");

            //    WDMethods.Sendkeys("PartnerEuTaxaccount", taxcode, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "taxeu beírva");

            //    WDMethods.Sendkeys("PartnerNote", partnernote, PropertyTypes.Id); 
            //    LogWriterT.WriteLog(folderpath, testname, "partner megjegyzés beírva");

            //    WDMethods.Click("SaveBtn", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "mentés katt");

            //    WDMethods.Click("Partnerreláció kiválasztása", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "Partnerreláció kiválasztása katt");

            //    WDMethods.Click("CellElement_3_0", PropertyTypes.Id);
            //    WDMethods.Click("CellElement_5_0", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "Partnerreláció kiválasztva ");
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.Click("Ok", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "reláció mentés");
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.Click("btClose", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "bezárás katt");

            //    /* beszállító rögzítése */

            //    WDMethods.WDClick("addPartner_btn", PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "hozzáadás katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("PartnerName", partner2, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "partner név beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("PartnerNickName", providnev2, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "partner rövidnév beírva");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);


            //    WDMethods.WDSendkeys("Keresés...", "Szolnok 5000", PropertyTypes.Name); ;
            //    LogWriterT.WriteLog(folderpath, testname, "város név beírva");
            //    WaitTime.Wait(1);
            //    SendKeys.SendWait("{Enter}");

            //    //WDMethods.WDClick("Szolnok 5000", PropertyTypes.Name); ;
            //    //LogWriterT.WriteLog(folderpath, testname, "város név katt");
            //    //WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("PartnerInvAddress", szamlazasiAddress2, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "város név beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("A számlázási cím megegyzik a levelezési címmel?", PropertyTypes.Name);
            //    WDMethods.WDClick("A számlázási cím megegyzik a levelezési címmel?", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "cím másolás be-kikapcsolás katt");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WDMethods.WDTextClear("PartnerCorAddress", PropertyTypes.Id);
            //    WDMethods.WDSendkeys("PartnerCorAddress", levelezesiAddress2, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "levelezési cím beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("PartnerEmail", partnermail2, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "email beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDScrollToElementAndClick("PartnerType", PropertyTypes.Id, "Társas vállalkozás");
            //    LogWriterT.WriteLog(folderpath, testname, "partner type kiválasztás");
            //    WaitTime.Wait(1);

            //    WDMethods.WDScrollToElementAndClick("PartnerCompanyForm", PropertyTypes.Id, "KFT.");
            //    LogWriterT.WriteLog(folderpath, testname, "partner form kiválasztás");
            //    WaitTime.Wait(1);

            //    WDMethods.WDScrollToElementAndClick("PartnerDepartmentType", PropertyTypes.Id, "Ipar, építõipar");
            //    LogWriterT.WriteLog(folderpath, testname, "ágazat kiválasztása");
            //    WaitTime.Wait(1);

            //    WDMethods.WDScrollToElementAndClick("PartnerPayClass", PropertyTypes.Id, "Jól fizetõ");
            //    LogWriterT.WriteLog(folderpath, testname, "fizetési minõsítés katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDScrollToElementAndClick("DefaultCurrency", PropertyTypes.Id, "HUF");
            //    LogWriterT.WriteLog(folderpath, testname, "alapértelmezett pénznem kiv.");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("PartnerTaxaccount", taxcode2, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "tax beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("PartnerEuTaxaccount", taxcode2, PropertyTypes.Id); ;
            //    LogWriterT.WriteLog(folderpath, testname, "taxeu beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("PartnerNote", partnernote, PropertyTypes.Id); ;

            //    LogWriterT.WriteLog(folderpath, testname, "megjegyzés beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("SaveBtn", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "mentés katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("Partnerreláció kiválasztása", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "Partnerreláció kiválasztása katt");
            //    WaitTime.Wait(3);

            //    WDMethods.WDClick("CellElement_3_0", PropertyTypes.Id);
            //    WaitTime.Wait(1);

            //    LogWriterT.WriteLog(folderpath, testname, "Partnerreláció kiválasztva ");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "reláció mentés");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("Alkatrész beszállító", PropertyTypes.Name);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("CheckBox", PropertyTypes.ClassName);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("Mentés", PropertyTypes.Name);
            //    WaitTime.Wait(2);
            //    WDMethods.WDClick("PART_CloseButton", PropertyTypes.Id);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("Szállító", PropertyTypes.Name);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("CellElement_1_0", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("Mentés", PropertyTypes.Name);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("PART_CloseButton", PropertyTypes.Id);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("btClose", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "bezárás katt");
            //    WaitTime.Wait(1);

            //    /*-------------------projekt rögzítése-------------------*/

            //    WDMethods.WDClick("Frissítés", PropertyTypes.Name);
            //    WDMethods.WDClick("Leállítás", PropertyTypes.Name);

            //    WDMethods.WDClick("MAIN_HOME_TAB", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "fõoldal katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("PROJECT_BROWSER", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "böngészõ katt");
            //    WaitTime.Wait(3);
            //    WDMethods.WDClick("Frissítés", PropertyTypes.Name);
            //    WaitTime.Wait(3);
            //    WDMethods.WDClick("Nem", PropertyTypes.Name);
            //    WaitTime.Wait(5);

            //    /*profil betöltése fõoldal/böngészõ*/

            //    WDMethods.WDRightClick("Partner", PropertyTypes.Id);
            //    WaitTime.Wait(3);
            //    WDMethods.WDClick("GridView profil betöltése", PropertyTypes.Name);
            //    WDMethods.WDClick("Releasetest", PropertyTypes.Name);
            //    WDMethods.WDClick("OkButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "profil betöltve");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("PART_ExpanderIcon", PropertyTypes.Id);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("PROJECT_BROWSER_ADDPROJ", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "projekt hozzáadás katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("toggleButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "projekt hozzáadás katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("FilterInputTexBox", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "keresõ katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("FilterInputTexBox", partner, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "partner beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("AttributeIcon", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "keresõ indítás katt");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "Ok gomb");
            //    WaitTime.Wait(1);

            //    WDMethods.WDScrollToElementAndClick("ProductTypeRCB", PropertyTypes.Id, "Projekt alapú gyártás");
            //    LogWriterT.WriteLog(folderpath, testname, "gyártás típus katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDScrollToElementAndClick("ProjectType", PropertyTypes.Id, "VEVÕI GENERÁLÁSSAL");
            //    LogWriterT.WriteLog(folderpath, testname, "projekt típus kiválasztva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("ProjectNationalTB", projektmegnkulsocode, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "projekt megnevezés");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("ContractNumTB", projektmegnkulsocode, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "szerzõdés szám");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("Code", projektmegnkulsocode, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "külsõ kód");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("ProjectCodeRefreshBT", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "code gen");
            //    WaitTime.Wait(1);



            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "Ok gomb");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("btClose", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "ablak bezárás");
            //    WaitTime.Wait(1);


            //    /*-------------------létrehozott projekt megnyitása-------------------*/


            //    WDMethods.WDSendkeys("ProjectNameTb", projektmegnkulsocode, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "projekt megnevezés beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("Frissítés", PropertyTypes.Name);

            //    WDMethods.WDDoubleClick(projektmegnkulsocode, PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "projekt katt");
            //    WaitTime.Wait(2);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    /*-------------------megrendeleés rögzítése projekt tab alatt------------------*/


            //    WDMethods.WDClick("MAIN_HOME_TAB", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "MAIN_HOME_TAB katt");
            //    WaitTime.Wait(1); ;

            //    WDMethods.WDClick("projectTab", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "projectTab projekt megnyitása");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("PROJECT_ORDER", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "PROJECT_ORDER megrendelés katt");
            //    WaitTime.Wait(3);

            //    WDMethods.WDClick("ORDER_ADDPO", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "ORDER_ADDPO megrendelés hozzáadás katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDTextClear("PoNumberTB", PropertyTypes.Id);
            //    WDMethods.WDSendkeys("PoNumberTB", projektmegnkulsocode, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "megrend szám beírása");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("ORDER_SAVEPO", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "mentés katt");
            //    WaitTime.Wait(3);

            //    WDMethods.WDClick("ItemEditBtn", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "ItemEditBtn katt");
            //    WaitTime.Wait(3);

            //    WDMethods.WDClick("PART_HeaderRow", PropertyTypes.Id);
            //    WDMethods.WDRightClick("PART_DropDownButton", PropertyTypes.Id);
            //    WDMethods.WDRightClick("GridView profil betöltése", PropertyTypes.Name);
            //    WDMethods.WDClick("Releasetest", PropertyTypes.Name);
            //    WDMethods.WDClick("OkButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "profil betöltve");
            //    WaitTime.Wait(2);

            //    /*cikk hozzáadása 1*/


            //    WDMethods.WDClick("ORDER_NEWPOITEM", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "ORDER_NEWPOITEM katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("Cell_0_3", "APFÕT", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk beírva");
            //    WaitTime.Wait(1);
            //    SendKeys.SendWait("{Enter}");
            //    WaitTime.Wait(2);

            //    WDMethods.WDClick("APFÕT 0404", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk kiválasztva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("CellElement_0_11", "2", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk mennyiség beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("CellElement_0_12", "358", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk egységár beírva");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    /*cikk hozzáadása 2*/

            //    WDMethods.WDClick("ORDER_NEWPOITEM", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "ORDER_NEWPOITEM katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("Cell_1_3", cikkszam, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk beírva");
            //    WaitTime.Wait(1);
            //    /*enteres kiválasztás cikkszám*/
            //    SendKeys.SendWait("{Enter}");
            //    LogWriterT.WriteLog(folderpath, testname, "cikk kiválasztva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("CellElement_1_11", megrendelesmennyiseg, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk mennyiség beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("CellElement_1_12", megrendelesegysegar, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk egységár beírva");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    //WDMethods.WDClick("CellElement_0_1", PropertyTypes.Id);
            //    //LogWriterT.WriteLog(folderpath, testname, "cikkszam2 checkbox katt");
            //    //WaitTime.Wait(1);

            //    WDMethods.WDClick("ORDER_NEWPOITEMS", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDSendkeys("textSource", "megrendcikk", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("AttributeIcon", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("CellElement_0_1", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("CellElement_1_1", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("CellElement_2_1", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    WaitTime.Wait(3);

            //    WDMethods.WDSendkeys("CellElement_2_11", "9", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk mennyiség beírva");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDSendkeys("CellElement_3_11", "11", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk mennyiség beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("CellElement_4_11", "10", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk mennyiség beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WDMethods.WDSendkeys("CellElement_4_12", "358", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk egységár beírva");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);


            //    WDMethods.WDClick("OrderDataTBI", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "megrendelés adataira katt");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("ORDER_SAVEPO", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "ORDER_SAVEPO megendelés mentés");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("ORDER_READYPO", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "ORDER_READYPO feldolgozva katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("btYes", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "btYes");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "Ok gomb");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("btYes", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "btYes");
            //    WaitTime.Wait(2);
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(5);
            //    WDMethods.TakePrtsc(folderpath, testname);


            //    /*-------------------sorzatgyártás/megrendelések/ beltöltés------------------*/


            //    WDMethods.WDClick("ProductionRadRibbonTab", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "logisztika tabra katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("SERIE_ORDER", PropertyTypes.Id);
            //    WaitTime.Wait(3);

            //    WDMethods.WDRightClick("PART_HeaderRow", PropertyTypes.Id);
            //    WDMethods.WDRightClick("GridView profil betöltése", PropertyTypes.Name);
            //    WDMethods.WDClick("Releasetest", PropertyTypes.Name);
            //    WDMethods.WDClick("OkButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "profil betöltve");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("ORDER_REFRESH", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "frisstés megtörtént");
            //    WaitTime.Wait(13);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    /*-------------------megrendelés anyagigénybõl------------------*/

            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WDMethods.WDClick("TRANSPORTMAINTENANCETAB", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "logisztika tabra katt");
            //    WaitTime.Wait(2);

            //    WDMethods.WDClick("ORDERS_OFFERS_MATERIAL", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "megrendelések, ajánlatok altab");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("3", PropertyTypes.Id); //érdekes ID!!!
            //    LogWriterT.WriteLog(folderpath, testname, "anyagigénye tab");
            //    WaitTime.Wait(7);

            //    /*profil betöltése anyagigények*/

            //    WDMethods.WDRightClick("PART_HeaderRow", PropertyTypes.Id);
            //    WaitTime.Wait(2);
            //    WDMethods.WDClick("GridView profil betöltése", PropertyTypes.Name);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("Releasetest", PropertyTypes.Name);
            //    WDMethods.WDClick("OkButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "profil betöltve");
            //    WaitTime.Wait(3);

            //    WDMethods.WDRightClick("PART_HeaderRow", PropertyTypes.Id);
            //    WaitTime.Wait(2);
            //    WDMethods.WDClick("GridView szûrõ váltás", PropertyTypes.Name);
            //    WaitTime.Wait(1);

            //    //WDMethods.WDRightClick("PART_DropDownButton", PropertyTypes.Id);
            //    //WDMethods.WDClick("Összes szûrõ törlése", PropertyTypes.Name);
            //    //WDMethods.WDRightClick("PART_DropDownButton", PropertyTypes.Id);
            //    //WDMethods.WDClick("GridView szûrõ váltás", PropertyTypes.Name);


            //    WDMethods.WDSendkeys("FilterStringEditor_11", projektmegnkulsocode, PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "szûrõbe írás");
            //    WaitTime.Wait(1);
            //    SendKeys.SendWait("{ENTER}");
            //    WaitTime.Wait(10);
            //    WDMethods.WDClick("CellElement_0_3", PropertyTypes.Id);
            //    WDMethods.WDClick("CellElement_1_3", PropertyTypes.Id);
            //    WDMethods.WDClick("CellElement_2_3", PropertyTypes.Id);
            //    WDMethods.WDClick("CellElement_3_3", PropertyTypes.Id);


            //    WDMethods.WDClick("OPEN_BRUTTO_SHEET_NEW_ORDER", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "rendelés indítása katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("toggleButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "szállító keresõ katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("FilterInputTexBox", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "keresõ katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("FilterInputTexBox", szallito, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "partner beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("AttributeIcon", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "keresõ indítás katt");
            //    WaitTime.Wait(2);

            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "Ok gomb");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("btYes", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("btYes", PropertyTypes.Id);
            //    WaitTime.Wait(1);

            //    WDMethods.WDScrollToElementAndClick("AccountInvoiceGroupSelector", PropertyTypes.Id, koltseghely);
            //    LogWriterT.WriteLog(folderpath, testname, "költséghely kiválasztása");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    WDMethods.WDClick("Rendelés lezárása", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "rendelés lezárása");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("btYes", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "rendelés lezárva");
            //    WaitTime.Wait(2);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("btClose", PropertyTypes.Id);

            //    /*-------------------új anyagigény rögzítése-------------------*/


            //    WDMethods.WDClick("NEW_OPEN_BRUTTO_SHEET_PARTS", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "anyagigény szerk. katt");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("toggleButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "projekt hozzáadás katt");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDSendkeys("textSource", cikkszam, PropertyTypes.Id);
            //    WaitTime.Wait(2);

            //    WDMethods.WDClick("AttributeIcon", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "keresõ indítás katt");
            //    WaitTime.Wait(5);
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WDMethods.WDDoubleClick("Ok", PropertyTypes.Name);

            //    WDMethods.WDSendkeys("DispQuantityNTB", megrendelesmennyiseg, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "mennyiség beírás");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("searchBox", projektmegnkulsocode, PropertyTypes.Id);
            //    WaitTime.Wait(3);
            //    SendKeys.SendWait("{Enter}");

            //    WDMethods.WDScrollToElementAndClick("SORCB", PropertyTypes.Id, cikkszam);
            //    WDMethods.WDScrollToElementAndClick("DepotSelector", PropertyTypes.Id, "IRODA");
            //    WDMethods.WDScrollToElementAndClick("StoreSelector", PropertyTypes.Id, "AALOGRAKJH");
            //    LogWriterT.WriteLog(folderpath, testname, "anyagigény mentés elõtt");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("Mentés és bezár", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "anyagigény mentés");
            //    WaitTime.Wait(4);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    /* anyag igény 2*/

            //    WDMethods.WDClick("NEW_OPEN_BRUTTO_SHEET_PARTS", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "anyagigény szerk. katt");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("toggleButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "cikk hozzáadás katt");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDSendkeys("textSource", cikkszam2, PropertyTypes.Id);
            //    WaitTime.Wait(2);

            //    WDMethods.WDClick("AttributeIcon", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "keresõ indítás katt");
            //    WaitTime.Wait(5);
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WDMethods.WDDoubleClick("Ok", PropertyTypes.Name);

            //    WDMethods.WDSendkeys("DispQuantityNTB", megrendelesmennyiseg, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "mennyiség beírás");
            //    WaitTime.Wait(1);

            //    WDMethods.WDScrollToElementAndClick("DepotSelector", PropertyTypes.Id, "IRODA");
            //    WDMethods.WDScrollToElementAndClick("StoreSelector", PropertyTypes.Id, "AALOGRAKJH");
            //    LogWriterT.WriteLog(folderpath, testname, "anyagigény mentés elõtt");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("Mentés és bezár", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "anyagigény mentés");
            //    WaitTime.Wait(4);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    /* megrendelés 2 indítása */

            //    WDMethods.WDRightClick("PART_HeaderRow", PropertyTypes.Id);
            //    WaitTime.Wait(2);
            //    WDMethods.WDClick("Összes szûrõ törlése", PropertyTypes.Name);
            //    WaitTime.Wait(1);
            //    WDMethods.WDSendkeys("FilterStringEditor_7", cikkszam2, PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "szûrõbe írás");
            //    WaitTime.Wait(1);
            //    SendKeys.SendWait("{ENTER}");
            //    WaitTime.Wait(10);
            //    WDMethods.WDClick("CellElement_0_3", PropertyTypes.Id);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("OPEN_BRUTTO_SHEET_NEW_ORDER", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "rendelés indítása katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("toggleButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "szállító keresõ katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("FilterInputTexBox", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "keresõ katt");
            //    WaitTime.Wait(1);

            //    WDMethods.WDSendkeys("FilterInputTexBox", partner2, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "partner beírva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("AttributeIcon", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "keresõ indítás katt");
            //    WaitTime.Wait(2);

            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "Ok gomb");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("btYes", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("btYes", PropertyTypes.Id);
            //    WaitTime.Wait(1);

            //    WDMethods.WDScrollToElementAndClick("AccountInvoiceGroupSelector", PropertyTypes.Id, koltseghely);
            //    LogWriterT.WriteLog(folderpath, testname, "költséghely kiválasztása");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    WDMethods.WDClick("Rendelés lezárása", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "rendelés lezárása");
            //    WaitTime.Wait(1);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("btYes", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "rendelés lezárva");
            //    WaitTime.Wait(2);
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    WDMethods.WDClick("btClose", PropertyTypes.Id);


            //    /* megrendelés rögzítése/

            //    //WDMethods.WDClick("ORDERS_BROWSER_ADDPO", PropertyTypes.Id);
            //    //WDMethods.WDClick("toggleButton", PropertyTypes.Id);
            //    //WDMethods.WDSendkeys("textSource", szallito, PropertyTypes.Id);
            //    //WDMethods.WDClick("AttributeIcon", PropertyTypes.Id); 
            //    //WDMethods.WDClick("Ok", PropertyTypes.Name); 
            //    //WDMethods.WDClick("btNo", PropertyTypes.Id);
            //    //WDMethods.WDScrollToElementAndClick("StoreSelector", PropertyTypes.Id, "AALOGRAKJH");
            //    //WDMethods.WDScrollToElementAndClick("AccountInvoiceGroupSelector", PropertyTypes.Id, "Test");
            //    //WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    //WDMethods.WDClick("",PropertyTypes.Name);


            //    /*-------------------betárolás megrendelés alapján-------------------*/


            //    WDMethods.WDClick("TRANSPORTMAINTENANCETAB", PropertyTypes.Id);
            //    WDMethods.WDClick("MATERIAL_STORE_IN", PropertyTypes.Id);
            //    WaitTime.Wait(2);

            //    /*profil betöltése betárolás*/

            //    WDMethods.WDRightClick("PART_HeaderRow", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("GridView profil betöltése", PropertyTypes.Name);
            //    WDMethods.WDClick("Releasetest", PropertyTypes.Name);
            //    WDMethods.WDClick("OkButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "profil betöltve");
            //    WaitTime.Wait(1);


            //    WDMethods.WDClick("PART_ExpanderIcon", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "profil betöltve");
            //    WaitTime.Wait(1);

            //    SendKeys.SendWait("{PGDN}");

            //    WDMethods.WDMoveToElement("MATERIAL_STORE_IN_ADD", PropertyTypes.Id);

            //    WDMethods.WDClick("MATERIAL_STORE_IN_ADD", PropertyTypes.Id);
            //    WaitTime.Wait(3);

            //    //WDMethods.WDSendkeys("AutoComplete", szallito, PropertyTypes.Id);
            //    //SendKeys.SendWait("{ENTER}");
            //    //PrtSc.TakePrtsc(folderpath, testname);
            //    //LogWriterT.WriteLog(folderpath, testname, "szállító kiválasztva");
            //    //WaitTime.Wait(1);

            //    WDMethods.WDClick("toggleButton", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDSendkeys("textSource", szallito, PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("AttributeIcon", PropertyTypes.Id);
            //    WaitTime.Wait(2);
            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "partner kiválasztva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("btYes", PropertyTypes.Id);

            //    WDMethods.WDSendkeys("CertifNoTB", szallitolevel, PropertyTypes.Id);
            //    WDMethods.WDSendkeys("DelivInvNoTB", szamlaszam, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "szállítólevél, számlaszám beírva");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("Mentés", PropertyTypes.Name);
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("Megrendelések", PropertyTypes.Name);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("allSelectCB", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "megrendelés tétel kiválasztva");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    WaitTime.Wait(1);

            //    if (WDMethods.driver.FindElement(By.Name("Figyelem")).Displayed && WDMethods.driver.FindElement(By.Name("Figyelem")).Enabled)
            //    {
            //        EgysegarmodBetar();
            //    }

            //    WDMethods.WDClick("storein_btn", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("btYes", PropertyTypes.Id);
            //    WaitTime.Wait(1);

            //    LogWriterT.WriteLog(folderpath, testname, "betárolás indítva");
            //    WDMethods.TakePrtsc(folderpath, testname);

            //    void EgysegarmodBetar()
            //    {
            //        while (true)
            //        {
            //            try
            //            {
            //                IWebElement figyelemElement = WDMethods.driver.FindElement(By.Name("Figyelem"));

            //                if (figyelemElement.Displayed)
            //                {
            //                    try
            //                    {
            //                        figyelemElement.Click();
            //                        WDMethods.WDClick("btYes", PropertyTypes.Id);
            //                        figyelemElement = WDMethods.driver.FindElement(By.Name("Figyelem"));

            //                    }
            //                    catch (Exception ex)
            //                    {
            //                        LogWriterT.WriteLog(folderpath, testname, $"Nem hajtható végre a gomb megnyomása: {ex.Message}");
            //                        //Log - exception!
            //                        break;
            //                    }
            //                }
            //                else
            //                {
            //                    //Ha az element nem kerül megjelenítésre
            //                    break; // Kilépés a ciklusból
            //                }
            //            }
            //            catch (NoSuchElementException)
            //            {
            //                //Ha nem található az element
            //                LogWriterT.WriteLog(folderpath, testname, "Element nem található");
            //                break; // Kilépés a ciklusból
            //            }
            //        }
            //    }


            //    WaitTime.Wait(2);

            //    /* betárolás 2 */

            //    WDMethods.WDMoveToElement("MATERIAL_STORE_IN_ADD", PropertyTypes.Id);

            //    WDMethods.WDClick("MATERIAL_STORE_IN_ADD", PropertyTypes.Id);
            //    WaitTime.Wait(3);

            //    WDMethods.WDSendkeys("AutoComplete", szallito, PropertyTypes.Id);
            //    SendKeys.SendWait("{ENTER}");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    LogWriterT.WriteLog(folderpath, testname, "szállító kiválasztva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("toggleButton", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDSendkeys("textSource", partner2, PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("AttributeIcon", PropertyTypes.Id);
            //    WaitTime.Wait(2);
            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "partner kiválasztva");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("btYes", PropertyTypes.Id);

            //    WDMethods.WDSendkeys("CertifNoTB", szallitolevel2, PropertyTypes.Id);
            //    WDMethods.WDSendkeys("DelivInvNoTB", szamlaszam2, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "szállítólevél, számlaszám beírva");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("Mentés", PropertyTypes.Name);
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("Megrendelések", PropertyTypes.Name);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("allSelectCB", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "megrendelés tétel kiválasztva");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    WaitTime.Wait(1);


            //    if (WDMethods.driver.FindElement(By.Name("Figyelem")).Displayed && WDMethods.driver.FindElement(By.Name("Figyelem")).Enabled)
            //    {
            //        EgysegarmodBetar();
            //    }


            //    WDMethods.WDClick("save_btn", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("storein_btn", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("btYes", PropertyTypes.Id);
            //    WaitTime.Wait(1);

            //    LogWriterT.WriteLog(folderpath, testname, "betárolás indítva");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(2);

            //    /* sztornó */
            //    WDMethods.WDSendkeys("FilterStringEditor_0", partner2, PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    SendKeys.SendWait("{ENTER}");
            //    WaitTime.Wait(2);
            //    WDMethods.WDDoubleClick(partner2, PropertyTypes.Name);
            //    WaitTime.Wait(1);
            //    WDMethods.Click("NEWSTOREINHEADSTORNO",PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.Click("btYes", PropertyTypes.Id);
            //    WaitTime.Wait(2);            
            //    LogWriterT.WriteLog(folderpath, testname, "betárolás sztorno");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WDMethods.Click("btClose", PropertyTypes.Id);
            //    WaitTime.Wait(1);

            //    /*-------------------Megnrendelés megnyitása-------------*/

            //    /*megrendelés profil betöltés*/

            //    WDMethods.WDClick("TRANSPORTMAINTENANCETAB", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "logisztika tabra katt");
            //    WaitTime.Wait(2);

            //    WDMethods.WDClick("ORDERS_OFFERS_MATERIAL", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "megrendelések, ajánlatok altab");
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("OrdersTab", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "megrendelések, ajánlatok/megrendelések ");
            //    WaitTime.Wait(2);

            //    WDMethods.WDRightClick("PART_HeaderRow", PropertyTypes.Id);
            //    WDMethods.WDRightClick("GridView profil betöltése", PropertyTypes.Name);
            //    WDMethods.WDClick("Releasetest", PropertyTypes.Name);
            //    WDMethods.WDClick("OkButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "profil betöltve");
            //    WaitTime.Wait(1);

            //    /*megrendelés megnyitása*/

            //    WDMethods.WDRightClick("PART_HeaderRow", PropertyTypes.Id);
            //    WaitTime.Wait(2);
            //    WDMethods.WDClick("GridView szûrõ váltás", PropertyTypes.Name);
            //    WaitTime.Wait(1);


            //    WDMethods.WDSendkeys("FilterStringEditor_2", partner2, PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "szûrõbe írás");
            //    WaitTime.Wait(1);
            //    SendKeys.SendWait("{ENTER}");
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("CellElement_0_2", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("btMaximize", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("CellElement_0_0", PropertyTypes.Id);
            //    WaitTime.Wait(1);
            //    WDMethods.WDClick("btClose", PropertyTypes.Id);
            //    WaitTime.Wait(1);

            //    /*-------------------vevõi értékesítés-------------------*/

            //    WDMethods.WDClick("TRANSPORTMAINTENANCETAB", PropertyTypes.Id);
            //    WDMethods.WDClick("CUST_ORDER", PropertyTypes.Id);
            //    WaitTime.Wait(4);

            //    /*profil betöltése vevõi értékesítés*/

            //    WDMethods.WDRightClick("PART_HeaderRow", PropertyTypes.Id);
            //    WDMethods.WDClick("GridView profil betöltése", PropertyTypes.Name);
            //    WDMethods.WDClick("Releasetest", PropertyTypes.Name);
            //    WDMethods.WDClick("OkButton", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "profil betöltve");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("CUST_ORDER_REFRESH", PropertyTypes.Id);
            //    WaitTime.Wait(4);

            //    WDMethods.WDSendkeys("FilterStringEditor_4", partner, PropertyTypes.Id);
            //    SendKeys.SendWait("{ENTER}");
            //    WaitTime.Wait(2);
            //    WDMethods.WDClick(partner, PropertyTypes.Name);
            //    WaitTime.Wait(4);
            //    WDMethods.WDClick(partner, PropertyTypes.Name);

            //    WDMethods.WDClick("CUST_ORDER_EDIT", PropertyTypes.Id);
            //    WaitTime.Wait(3);
            //    WDMethods.WDClick("Mentés", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "vevõi értékesítés megnyitva");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(3);


            //    WDMethods.WDClick("Kitárolás", PropertyTypes.Name);
            //    WaitTime.Wait(1);
            //    WDMethods.WDMoveToElement("Popup", PropertyTypes.ClassName);
            //    WDMethods.WDClick("RadRibbonButton", PropertyTypes.ClassName);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("allSelectCB", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "kitárolás tétel/tételek kiválasztva");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(1);


            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    WaitTime.Wait(3);
            //    LogWriterT.WriteLog(folderpath, testname, "kitárolás indítva");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(4);

            //    /*göngyöleg!*/
            //    //if (WDMethods.driver.FindElement(WiniumBy.Id("btClose").Displayed)
            //    //{
            //    //    WDMethods.WDClick("btClose", PropertyTypes.Id);
            //    //    WaitTime.Wait(2);
            //    //}

            //    if (WDMethods.driver.FindElement(By.Name("Figyelmeztetés")).Displayed && WDMethods.driver.FindElement(By.Name("Figyelmeztetés")).Enabled)
            //    {
            //        StimulSoftPrint();
            //    }
            //    else
            //    {
            //        CrystalPrint();

            //    }

            //    void StimulSoftPrint()
            //    {
            //        //WaitTime.FluentWait();
            //        LogWriterT.WriteLog(folderpath, testname, "nem crystalriport nyomtatás történt");
            //        WDMethods.WDClick("Figyelmeztetés", PropertyTypes.Name);
            //        WaitTime.Wait(2);
            //        WDMethods.WDClick("btNo", PropertyTypes.Id);
            //        WaitTime.Wait(2);
            //        WDMethods.WDClick("Ok", PropertyTypes.Name);
            //        WaitTime.Wait(3);
            //        LogWriterT.WriteLog(folderpath, testname, "szállítólevél nyomtatva");
            //        WaitTime.Wait(1);
            //        WDMethods.WDClick("scrollViewer", PropertyTypes.Id);
            //        WaitTime.Wait(1);
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WDMethods.WDClick("TitleBar", PropertyTypes.Id);
            //        WDMethods.WDClick("Close", PropertyTypes.Id);
            //        WaitTime.Wait(1);
            //    }

            //    void CrystalPrint()
            //    {
            //        LogWriterT.WriteLog(folderpath, testname, "nem stimulsoft nyomtatás történt");
            //        WDMethods.WDClick("TitleBar", PropertyTypes.Id);
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WDMethods.WDClick("Close", PropertyTypes.Id);
            //        WaitTime.Wait(1);
            //    }


            //    /*-------------------számlázás-------------------*/

            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WDMethods.WDClick("Számlázás", PropertyTypes.Name);
            //    WaitTime.Wait(1);
            //    WDMethods.WDMoveToElement("Popup", PropertyTypes.ClassName);
            //    WDMethods.WDClick("RadRibbonButton", PropertyTypes.ClassName);
            //    WaitTime.Wait(1);

            //    WDMethods.WDClick("Kitárolt", PropertyTypes.Name);
            //    WDMethods.WDClick("Számla készítése", PropertyTypes.Name);
            //    LogWriterT.WriteLog(folderpath, testname, "végszámla készítés");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(2);

            //    if (WDMethods.driver.FindElement(By.Name("Kérdés")).Displayed && WDMethods.driver.FindElement(By.Name("Kérdés")).Enabled)
            //    {
            //        Egysegarmod2();
            //    }

            //    void Egysegarmod2()
            //    {
            //        while (WDMethods.driver.FindElement(By.Name("Kérdés")).Displayed && WDMethods.driver.FindElement(By.Name("Kérdés")).Enabled)
            //        {
            //            WDMethods.WDClick("btNo", PropertyTypes.Id);
            //        }
            //    }

            //    WaitTime.Wait(2);

            //    if (WDMethods.driver.FindElement(By.Name("Figyelmeztetés")).Displayed && WDMethods.driver.FindElement(By.Name("Figyelmeztetés")).Enabled)
            //    {
            //        SzámlázásMethodStimulsoft();
            //        LogWriterT.WriteLog(folderpath, testname, "nem crystalriport nyomtatás történt");
            //    }
            //    else
            //    {
            //        SzámlázásMethodCrystal();
            //        LogWriterT.WriteLog(folderpath, testname, "nem stimulsoft nyomtatás történt");
            //    }


            //    void SzámlázásMethodStimulsoft()
            //    {

            //        WDMethods.WDClick("btNo", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "riport kiválasztás");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        WDMethods.WDClick("toggleButton", PropertyTypes.Id);
            //        WaitTime.Wait(2);
            //        WDMethods.WDClick("CellElement_0_0", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "bankszámla kiválasztás");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(2);

            //        WDMethods.WDClick("SelectCommand", PropertyTypes.Id);
            //        WDMethods.WDClick("Ok", PropertyTypes.Name);
            //        WaitTime.Wait(2);
            //        WDMethods.WDClick("Ok", PropertyTypes.Name);
            //        WaitTime.Wait(2);
            //        WDMethods.WDClick("btOK", PropertyTypes.Id);
            //        WaitTime.Wait(3);

            //        WDMethods.WDClick("btNo", PropertyTypes.Id);
            //        WDMethods.WDClick("btNo", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "kiválasztás");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(2);

            //        WDMethods.WDClick("Ok", PropertyTypes.Name);
            //        WaitTime.Wait(5);
            //        LogWriterT.WriteLog(folderpath, testname, "számla nyomtatva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(2);

            //        WDMethods.WDClick("Figyelmeztetés", PropertyTypes.Name);
            //        WDMethods.WDClick("btNo", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "kiválasztás");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(2);
            //        WDMethods.WDClick("Ok", PropertyTypes.Name);
            //        WaitTime.Wait(1);

            //        WDMethods.WDClick("scrollViewer", PropertyTypes.Id);
            //        WaitTime.Wait(1);

            //        WDMethods.WDClick("TitleBar", PropertyTypes.Id);
            //        WDMethods.WDClick("Close", PropertyTypes.Id);
            //        WaitTime.Wait(1);

            //    }

            //    void SzámlázásMethodCrystal()
            //    {
            //        WDMethods.WDClick("toggleButton", PropertyTypes.Id);
            //        WaitTime.Wait(2);
            //        WDMethods.WDClick("CellElement_0_0", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "bankszámla kiválasztás");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(2);

            //        WDMethods.WDClick("SelectCommand", PropertyTypes.Id);
            //        WDMethods.WDClick("Ok", PropertyTypes.Name);
            //        WDMethods.WDClick("btOK", PropertyTypes.Id);
            //        WaitTime.Wait(3);

            //        WDMethods.WDClick("btNo", PropertyTypes.Id);
            //        WDMethods.WDClick("btNo", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "kiválasztás");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(2);

            //        WDMethods.WDClick("Ok", PropertyTypes.Name);
            //        WDMethods.WDClick("Win32", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "számla nyomtatva");
            //        WaitTime.Wait(1);
            //        WDMethods.WDClick("2", PropertyTypes.Id);
            //        WaitTime.Wait(1);
            //    }

            //    WDMethods.WDClick("btClose", PropertyTypes.Id);

            //    /*-------------------munkakiosztás-------------------*/


            //    /*napimunkarend meghatározása*/
            //    WDMethods.WDClick("ProductionRadRibbonTab", PropertyTypes.Id);
            //    WDMethods.WDClick("SERIE_RESOURCE", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "nap mûszakrend kiválasztva");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(5);

            //    WDMethods.WDScrollToElementAndClick("SectionsComboBox", PropertyTypes.Id, reszleg);
            //    LogWriterT.WriteLog(folderpath, testname, "részleg kiválasztva");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(5);

            //    WDMethods.WDClick("CAPACITY_GENERATE", PropertyTypes.Id);
            //    WaitTime.Wait(2);

            //    WDMethods.WDClick("PART_DropDownButton", PropertyTypes.Id);
            //    WDMethods.WDSendkeys("Filter0TextBoxEditor", munkatárs, PropertyTypes.Id);
            //    WDMethods.WDClick("PART_ApplyFilterButton", PropertyTypes.Id);
            //    WDMethods.WDClick("allSelectCB", PropertyTypes.Id);
            //    WDMethods.WDDoubleClick("PART_RadComboBox", PropertyTypes.Id);
            //    WDMethods.WDSendkeys("TextBox", muszakrend, PropertyTypes.ClassName);

            //    /*minden felhasználóra való generálás, gond lehet ezzel, ha van keresztezõ mûszakrend!!!*/
            //    //WDMethods.WDClick("allSelectCB", PropertyTypes.Id);
            //    //LogWriterT.WriteLog(folderpath, testname, "dolgozók kiválasztva");
            //    //PrtSc.TakePrtsc(folderpath, testname);
            //    //WaitTime.Wait(5);
            //    //WDMethods.WDDoubleClick("CellElement_0_6", PropertyTypes.Id);
            //    //WDMethods.WDClick("CellElement_0_6", PropertyTypes.Id);            
            //    //WDMethods.WDSendkeys("TextBox", muszakrend, PropertyTypes.ClassName);
            //    //SendKeys.SendWait("{ENTER}");
            //    //LogWriterT.WriteLog(folderpath, testname, "mûszakrend kiválasztva");
            //    //PrtSc.TakePrtsc(folderpath, testname);
            //    //WaitTime.Wait(5);

            //    WDMethods.WDClick("Ok", PropertyTypes.Name);
            //    WDMethods.WDClick("CAPACITY_SAVE", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "mûszakrend mentve");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(5);



            //    /*munkakiosztás*/
            //    WDMethods.WDClick("ProductionRadRibbonTab", PropertyTypes.Id);
            //    WDMethods.WDClick("SERIEPROD_WORKALLOC", PropertyTypes.Id);
            //    WDMethods.WDClick("WORK_ALLOCATION", PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "gyártás, manuális munkakiosztás");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(5);

            //    WDMethods.WDClick("Gyártás munkakiosztás", PropertyTypes.Name);
            //    WDMethods.WDSendkeys("PART_DateTimeInput", today, PropertyTypes.Id);
            //    LogWriterT.WriteLog(folderpath, testname, "dátum beírása");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(5);
            //    WDMethods.WDScrollToElementAndClick("ShiftRCB", PropertyTypes.Id, muszakrend);
            //    LogWriterT.WriteLog(folderpath, testname, "mûszakrend kiválasztva");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(5);
            //    WDMethods.WDScrollToElementAndClick("SectionRCB", PropertyTypes.Id, reszleg);
            //    LogWriterT.WriteLog(folderpath, testname, "részleg kiválasztva");
            //    WDMethods.TakePrtsc(folderpath, testname);
            //    WaitTime.Wait(5);

            //    if (!(!WDMethods.driver.FindElement(By.Name("WORKALLOC_ADD")).Displayed || !WDMethods.driver.FindElement(By.Name("WORKALLOC_ADD")).Enabled))
            //    {
            //        Method1Munkakiosztás();
            //    }
            //    else
            //    {
            //        if (WDMethods.driver.FindElement(By.Name("WORKALLOC_MODIFY")).Displayed && WDMethods.driver.FindElement(By.Name("WORKALLOC_MODIFY")).Enabled)
            //        {
            //            Method2Munkakiosztás();
            //        }
            //        else
            //        {
            //            Method3Munkakiosztás();
            //        }
            //    }

            //    void Method1Munkakiosztás()
            //    {
            //        /*ha nincs aktív munkakiosztás és aktív a workalloc_add akkor ide fut bele*/
            //        WDMethods.WDClick("WORKALLOC_ADD", PropertyTypes.Id);
            //        WDMethods.WDClick("WORKALLOC_ITEMADD", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "munkakiosztás indítása");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        WDMethods.WDSendkeys("Keresés...", "APFÕT 0404", PropertyTypes.Name);
            //        SendKeys.SendWait("{ENTER}");
            //        LogWriterT.WriteLog(folderpath, testname, "cikkszám kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(2);

            //        WDMethods.WDScrollToElementAndClick("OrderItemRCB", PropertyTypes.Id, projektmegnkulsocode);
            //        LogWriterT.WriteLog(folderpath, testname, "megrendelés kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(3);

            //        WDMethods.WDClick("OperationRCB", PropertyTypes.Id);
            //        WaitTime.Wait(1);
            //        WDMethods.WDClick("MARÁS - 1", PropertyTypes.Name);
            //        LogWriterT.WriteLog(folderpath, testname, "mûvelet kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        //WDMethods.WDScrollToElementAndClick("OperationRCB", PropertyTypes.Id, "MARÁS - 1");
            //        //WDMethods.WDScrollToElementAndClick("StationRCB", PropertyTypes.Id, "Maró III.");

            //        WDMethods.WDClick("StationRCB", PropertyTypes.Id);
            //        WDMethods.WDClick("Maró GÉP 1", PropertyTypes.Name);
            //        LogWriterT.WriteLog(folderpath, testname, "munkaállomás kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        WDMethods.WDRightClick("Beosztás", PropertyTypes.Name);
            //        WDMethods.WDClick("Hozzáadás", PropertyTypes.Name);
            //        WDMethods.WDClick("CellElement_0_0", PropertyTypes.Id);
            //        WDMethods.WDClick("WorkAllocUserPopUpOKBtn", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "munkatárs kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        WDMethods.WDClick("WorkAllocOKBTn", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "munkakiosztás mentése");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        WDMethods.WDClick("WORKALLOC_ACTIVATE", PropertyTypes.Id);
            //        WDMethods.WDClick("btYes", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "munkakiosztás aktiválása");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);
            //    }
            //    void Method2Munkakiosztás()
            //    {
            //        /*ha már van aktív munkakiosztás akkor ebbe fut bele*/
            //        WDMethods.WDClick("WORKALLOC_MODIFY", PropertyTypes.Id);
            //        WDMethods.WDClick("btYes", PropertyTypes.Id);
            //        WDMethods.WDClick("Ok", PropertyTypes.Name);
            //        WDMethods.WDClick("WORKALLOC_ITEMADD", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "munkakiosztás indítása");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        WDMethods.WDSendkeys("Keresés...", "APFÕT 0404", PropertyTypes.Name);
            //        SendKeys.SendWait("{ENTER}");
            //        LogWriterT.WriteLog(folderpath, testname, "cikk kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(2);

            //        WDMethods.WDScrollToElementAndClick("OrderItemRCB", PropertyTypes.Id, projektmegnkulsocode);
            //        LogWriterT.WriteLog(folderpath, testname, "megrendelés kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(3); ;

            //        WDMethods.WDClick("OperationRCB", PropertyTypes.Id);
            //        WaitTime.Wait(1);
            //        WDMethods.WDClick("MARÁS - 1", PropertyTypes.Name);
            //        LogWriterT.WriteLog(folderpath, testname, "mûvelet kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        //WDMethods.WDScrollToElementAndClick("OperationRCB", PropertyTypes.Id, "MARÁS - 1");
            //        //WDMethods.WDScrollToElementAndClick("StationRCB", PropertyTypes.Id, "Maró III.");

            //        WDMethods.WDClick("StationRCB", PropertyTypes.Id);
            //        WDMethods.WDClick("Maró GÉP 1", PropertyTypes.Name);
            //        LogWriterT.WriteLog(folderpath, testname, "munkaállomás kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        WDMethods.WDRightClick("Beosztás", PropertyTypes.Name);
            //        WDMethods.WDClick("Hozzáadás", PropertyTypes.Name);
            //        WDMethods.WDClick("CellElement_0_0", PropertyTypes.Id);
            //        WDMethods.WDClick("WorkAllocUserPopUpOKBtn", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "munkatárs kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        WDMethods.WDClick("WorkAllocOKBTn", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "munkakiosztás mentése");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        WDMethods.WDClick("WORKALLOC_ACTIVATE", PropertyTypes.Id);
            //        WDMethods.WDClick("btYes", PropertyTypes.Id);
            //        WDMethods.WDClick("btNo", PropertyTypes.Id);
            //        WDMethods.WDClick("btYes", PropertyTypes.Id);
            //        WDMethods.WDClick("btOK", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "munkakiosztás aktiválása");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);
            //    }
            //    void Method3Munkakiosztás()
            //    {
            //        /*ha nem aktív a workalloc_add ide fut bele*/
            //        WDMethods.WDClick("WORKALLOC_ITEMADD", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "munkakiosztás indítása");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        WDMethods.WDSendkeys("Keresés...", "APFÕT 0404", PropertyTypes.Name);
            //        SendKeys.SendWait("{ENTER}");
            //        LogWriterT.WriteLog(folderpath, testname, "cikk kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(2);

            //        WDMethods.WDScrollToElementAndClick("OrderItemRCB", PropertyTypes.Id, projektmegnkulsocode);
            //        LogWriterT.WriteLog(folderpath, testname, "megrendelés kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(3);

            //        WDMethods.WDClick("OperationRCB", PropertyTypes.Id);
            //        WaitTime.Wait(1);
            //        WDMethods.WDClick("MARÁS - 1", PropertyTypes.Name);
            //        LogWriterT.WriteLog(folderpath, testname, "mûvelet kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        WDMethods.WDClick("StationRCB", PropertyTypes.Id);
            //        WDMethods.WDClick("Maró GÉP 1", PropertyTypes.Name);
            //        LogWriterT.WriteLog(folderpath, testname, "munkaállomás kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        //WDMethods.WDScrollToElementAndClick("OperationRCB", PropertyTypes.Id, "MARÁS - 1");
            //        //WDMethods.WDScrollToElementAndClick("StationRCB", PropertyTypes.Id, "Maró III.");                               

            //        WDMethods.WDRightClick("Beosztás", PropertyTypes.Name);
            //        WDMethods.WDClick("Hozzáadás", PropertyTypes.Name);
            //        WDMethods.WDClick("CellElement_0_0", PropertyTypes.Id);
            //        WDMethods.WDClick("WorkAllocUserPopUpOKBtn", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "munkatárs kiválasztva");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        WDMethods.WDClick("WorkAllocOKBTn", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "munkakiosztás mentése");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);

            //        WDMethods.WDClick("WORKALLOC_ACTIVATE", PropertyTypes.Id);
            //        WDMethods.WDClick("btYes", PropertyTypes.Id);
            //        WDMethods.WDClick("btNo", PropertyTypes.Id);
            //        WDMethods.WDClick("btYes", PropertyTypes.Id);
            //        WDMethods.WDClick("btOK", PropertyTypes.Id);
            //        LogWriterT.WriteLog(folderpath, testname, "munkakiosztás aktiválása");
            //        WDMethods.TakePrtsc(folderpath, testname);
            //        WaitTime.Wait(1);



            //        //WDMethods.WDSendkeys("textSource", "MEGRENDCIKK5", PropertyTypes.Id);
            //        //WDMethods.Click("AttributeIcon", PropertyTypes.Id);
            //        //WaitTime.Wait(2);


            //        //WDMethods.Start(appPath, winiumDriverDirectory);
            //        //WaitTime.Wait(2);
            //        //WDMethods.WDClick("Ugrás a mai napra", PropertyTypes.Name);
            //        ////WDMethods.WDClick("07:25", PropertyTypes.Name);
            //        //WaitTime.Wait(2);
            //        //WDMethods.DragAndDrop("PART_TimeRulerPanel", "07:55", "08:55");


            //}
        }
    }
}