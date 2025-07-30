using Microsoft.Web.WebView2.Core;
using System;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Windows;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace TestAutomationUI
{
    public partial class ElementInfoWindow : Window
    {
        private string _urlToInspect;

        public ElementInfoWindow(string urlToInspect)
        {
            InitializeComponent();
            _urlToInspect = urlToInspect;
            this.Loaded += ElementInfoWindow_Loaded;
        }


        private async void ElementInfoWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await webView.EnsureCoreWebView2Async();

            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            webView.NavigationCompleted += WebView_NavigationCompleted;

            webView.Source = new Uri(_urlToInspect);
        }

        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {

            string jsCode = "(function () {\r\n    function getXPath(el) {\r\n        if (el.id !== '') return 'id(\\\"' + el.id + '\\\")';\r\n        if (el === document.body) return '/html/body';\r\n        let ix = 0;\r\n        let siblings = el.parentNode.childNodes;\r\n        for (let i = 0; i < siblings.length; i++) {\r\n            let sibling = siblings[i];\r\n            if (sibling === el) {\r\n                return getXPath(el.parentNode) + '/' + el.tagName.toLowerCase() + '[' + (ix + 1) + ']';\r\n            }\r\n            if (sibling.nodeType === 1 && sibling.tagName === el.tagName) {\r\n                ix++;\r\n            }\r\n        }\r\n    }\r\n\r\n    function getCssSelector(el) {\r\n        if (el.id) return `#${el.id}`;\r\n        const path = [];\r\n        while (el.parentElement) {\r\n            let selector = el.tagName.toLowerCase();\r\n            if (el.className) {\r\n                selector += '.' + el.className.trim().replace(/\\s+/g, '.');\r\n            }\r\n            const siblings = Array.from(el.parentElement.children).filter(e => e.tagName === el.tagName);\r\n            if (siblings.length > 1) {\r\n                selector += `:nth-of-type(${Array.from(el.parentElement.children).indexOf(el) + 1})`;\r\n            }\r\n            path.unshift(selector);\r\n            el = el.parentElement;\r\n        }\r\n        return path.join(' > ');\r\n    }\r\n\r\n    function onClick(e) {\r\n        e.preventDefault();\r\n        e.stopPropagation();\r\n        const el = e.target;\r\n        const info = {\r\n            tag: el.tagName.toLowerCase(),\r\n            id: el.id || '',\r\n            class: el.className || '',\r\n            name: el.getAttribute('name') || '',\r\n            xpath: getXPath(el),\r\n            cssSelector: getCssSelector(el),\r\n            linkText: el.innerText || '',\r\n            partialLinkText: (el.innerText || '').substring(0, 10)\r\n        };\r\n        window.chrome.webview.postMessage(info);\r\n    }\r\n\r\n    document.body.style.cursor = 'crosshair';\r\n    document.addEventListener('click', onClick, true);\r\n})();";



            await webView.ExecuteScriptAsync(jsCode);
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.WebMessageAsJson;
                var el = JsonSerializer.Deserialize<WebElementInfo>(json);

                string msg = $"DOM elem adatai:\n\n" +
                             $"Tag: {el.tag}\n" +
                             $"ID: {el.id}\n" +
                             $"Class: {el.@class}\n" +
                             $"Name: {el.name}\n" +
                             $"XPath: {el.xpath}\n" +
                             $"CSS Selector: {el.cssSelector}\n" +
                             $"Link Text: {el.linkText}\n" +
                             $"Partial Link Text: {el.partialLinkText}";

                //MessageBox.Show(msg, "WEB Inspect");
                DetailsTextBox.Text = msg;

            }
            catch (Exception ex)
            {
                MessageBox.Show("Hiba az elem adatainak feldolgozásánál: " + ex.Message);
            }
        }


        public void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public class WebElementInfo
        {
            public string tag { get; set; }
            public string id { get; set; }
            public string @class { get; set; }
            public string name { get; set; }
            public string xpath { get; set; }
            public string cssSelector { get; set; }
            public string linkText { get; set; }
            public string partialLinkText { get; set; }
        }
    }
}
