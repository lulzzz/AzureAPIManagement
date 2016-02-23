﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmallStepsLabs.Azure.ApiManagement;
using SmallStepsLabs.Azure.ApiManagement.Model;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.ApiManagement.Test
{
    [TestClass]
    public class ManagementClientTests
    {
        protected ManagementClient Client { get; set; }

        [TestInitialize]
        public void Setup()
        {
            var host = "devav";
            var serviceId = "56ca4cd99e1436035c030003";
            var accessKey = "NaS+Pv8qOESQ3H4HvnVz3JEQRVq16sVCgnkW+7ldqaT8cIqcKfFe089bSZUnhyHhVu1BXXEz0udjHHEh1w6JBw==";

            this.Client = new ManagementClient(host, serviceId, accessKey);
        }

        #region Arguments Validation

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void InvalidArgumentGetProduct()
        {
            Client.GetProductAsync(null).Wait();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void InvalidArgumentCreateProduct()
        {
            Client.CreateProductAsync(null).Wait();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void InvalidArgumentIdCreateProduct()
        {
            Client.CreateProductAsync(new Product()).Wait();
        }

        #endregion

        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public void CancelRequest()
        {
            var cts = new CancellationTokenSource();
            var task = Client.GetProductsAsync(cancellationToken: cts.Token);

            cts.CancelAfter(250);
            task.Wait(cts.Token);
        }

        [TestMethod]
        public void GetProducts()
        {
            var task = Client.GetProductsAsync();
            task.Wait();

            Assert.AreNotEqual(null, task.Result);
        }

        [TestMethod]
        public void GetProductsExpandGroups()
        {
            var task = Client.GetProductsAsync(expandGroups: true);
            task.Wait();

            Assert.AreEqual(true, task.Result.Any(p => p.Groups != null));
        }

        [TestMethod]
        public void GetProduct()
        {
            var task = Client.GetProductsAsync();
            task.Wait();

            var productId = task.Result[0].Id;

            var pTask = Client.GetProductAsync(productId);

            Assert.AreNotEqual(null, pTask.Result);
            Assert.AreNotEqual(productId, pTask.Id);
        }

        [TestMethod]
        public void GetProductByFilter()
        {
            var task = Client.GetProductsAsync();
            task.Wait();

            var productId = task.Result.Last().Id;

            var filter = String.Format("id eq '{0}'", productId);
            var pTask = Client.GetProductsAsync(filter);
            pTask.Wait();

            Assert.IsTrue(pTask.Result.Count == 1);
            Assert.AreEqual(productId, pTask.Result.First().Id);
        }

        [TestMethod]
        public void UpdateProduct()
        {
            var task = Client.GetProductsAsync();
            task.Wait();

            var product = task.Result.First();

            var testDesc = DateTime.Now.ToLongTimeString();

            product.Description = testDesc;

            Client.UpdateProductAsync(product).Wait();

            var pickTask = Client.GetProductAsync(product.Id);
            pickTask.Wait();

            Assert.AreEqual(testDesc, pickTask.Result.Description);
        }

        [TestMethod]
        public void CreateProduct()
        {
            var id = Guid.NewGuid().ToString("N");
            var newProduct = new Product()
            {
                Id = id,
                Name = "Testing product " + id,
                Description = "Testing"
            };

            Client.CreateProductAsync(newProduct).Wait();

            var pickTask = Client.GetProductAsync(newProduct.Id);
            pickTask.Wait();

            Assert.AreEqual(newProduct.Id, pickTask.Result.Id);
        }

        [TestMethod]
        [ExpectedException(typeof(HttpResponseException))]
        public void DeleteProduct()
        {
            var id = Guid.NewGuid().ToString("N");
            var newProduct = new Product()
            {
                Id = id,
                Name = "Testing product " + id,
                Description = "Testing"
            };

            Client.CreateProductAsync(newProduct).Wait();
            Client.DeleteProductAsync(newProduct.Id, true).Wait();

            try
            {

                Client.GetProductAsync(newProduct.Id).Wait();
            }
            catch(AggregateException ex)
            {
                throw ex.InnerException;
            }
        }


        [TestMethod]
        public void GetProductAPIs()
        {
            var task = Client.GetProductsAsync();
            task.Wait();

            var productId = task.Result[0].Id;

            var pTask = Client.GetProductAPIsAsync(productId);
            pTask.Wait();

            Assert.AreNotEqual(null, pTask.Result);
        }

        [TestMethod]
        public void GetProductAddRemoveAPIs()
        {
            var task = Client.GetProductsAsync();
            task.Wait();

            var productId = task.Result[0].Id;

            var pTask = Client.GetProductAPIsAsync(productId);
            pTask.Wait();

            if(pTask.Result.Count > 0)
            {
                var apiId = pTask.Result[0].Id;
            }
        }
    }
}
