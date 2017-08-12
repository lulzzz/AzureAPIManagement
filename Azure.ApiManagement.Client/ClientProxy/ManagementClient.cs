﻿using Fitabase.Azure.ApiManagement.DataModel.Properties;
using Fitabase.Azure.ApiManagement.Model;
using Fitabase.Azure.ApiManagement.Model.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Fitabase.Azure.ApiManagement
{
    public class ManagementClient
    {
        //static readonly string user_agent = "Fitabase/v1";
        public static readonly int RatesReqTimeout = 25;
        public static readonly int TransactionReqTimeOut = 25;
        static readonly Encoding encoding = Encoding.UTF8;

        static string _api_endpoint;
        static string _serviceId;
        static string _accessToken;
        static string _apiVersion;

        public string GetEndpoint()
        {
            return _api_endpoint;
        }



        public int TimeoutSeconds { get; set; }


        public ManagementClient(string host, string serviceId, string accessToken)
            : this(host, serviceId, accessToken, Constants.ApiManagement.Versions.Feb2014)
        {

        }

        public ManagementClient(string host, string serviceId, string accessToken, string apiversion)
        {
            _api_endpoint = host;
            _serviceId = serviceId;
            _accessToken = accessToken;
            _apiVersion = apiversion;
            TimeoutSeconds = 25;
        }

        public ManagementClient(string filePath)
        {
            Init(filePath);
            TimeoutSeconds = 25;
        }

        /// <summary>
        /// Read and initialize keys
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private void Init(string filePath)
        {
            string apiKeysContent;
            try
            {
                using (StreamReader sr = new StreamReader(filePath)) //make sure this file has "Copy to output directory" Set to "Copy Always"
                {
                    apiKeysContent = sr.ReadToEnd();
                    var json = JObject.Parse(apiKeysContent);
                    _api_endpoint = json["apiEndpoint"].ToString();
                    _serviceId = json["serviceId"].ToString();
                    _accessToken = json["accessKey"].ToString();
                    _apiVersion = json["apiVersion"].ToString();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Ensure the endpoint is properly formatted
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string GetFormatedEndpoint(string url)
        {
            StringBuilder builder = new StringBuilder();

            // ensure the url contains the api endpoint with proper format
            // url = _api_endpoint/request_endpoint
            if (!url.Contains(_api_endpoint))
            {
                if (url.StartsWith("/"))
                {
                    builder.Append(_api_endpoint);
                }
                else
                {
                    builder.Append(_api_endpoint).Append("/");
                }
                builder.Append(url);
            }
            else
            {
                builder.Append(url);
            }

            // ensure the url contains the api version 
            // url = _api_endpoint/request_endpoint?api-version=_apiVersion
            // or url = _api_endpoint/request_endpoint?params&api-version=_apiVersion
            if (!url.Contains(_apiVersion))
            {
                if (url.Contains("?"))
                {
                    if (url.EndsWith("?"))
                    {
                        builder.Append("api-version=").Append(_apiVersion);
                    }
                    else
                    {
                        builder.Append("&api-version=").Append(_apiVersion);
                    }
                }
                else
                {
                    builder.Append("?api-version=").Append(_apiVersion);
                }
            }
            return builder.ToString();
        }


        protected virtual HttpRequestMessage GetRequest(String method, string uri, string body = null)
        {
            string endpointURI = GetFormatedEndpoint(uri);
            string token = Utility.CreateSharedAccessToken(_serviceId, _accessToken, DateTime.UtcNow.AddDays(1));
            
            HttpMethod httpMethod = new HttpMethod(method);
            HttpRequestMessage request = new HttpRequestMessage(httpMethod, endpointURI);
            HttpContent content = null;
            

            if (method == RequestMethod.POST.ToString() || method == RequestMethod.PUT.ToString())
            {
                if (body != null)
                {
                    content = new StringContent(body, Encoding.UTF8, "application/json");
                }
            }
            else if (method == RequestMethod.PATCH.ToString())
            {
                content = new StringContent(body, Encoding.UTF8, "application/json");
                request.Headers.Add("If-Match", "*");
            }
            else if (method == RequestMethod.DELETE.ToString())
            {
                request.Headers.Add("If-Match", "*");
            }

            request.Headers.Add("Authorization", Constants.ApiManagement.AccessToken + " " + token);
            request.Headers.Add("api-version", _apiVersion);
            request.Content = content;

            return request;
        }
        

        #region Generic Requests

        public virtual async Task<T> DoRequestAsync<T>(string endpoint, RequestMethod method = RequestMethod.GET, string body = null)
        {
            //var json = DoRequest(endpoint, method.ToString(), body);
            string json = await DoRequestAsync(endpoint, method.ToString(), body);
            System.Diagnostics.Debug.WriteLine(json);
            if (String.IsNullOrWhiteSpace(json))
            {
                return default(T);
            }
            T obj = JsonConvert.DeserializeObject<T>(json);
            return obj;
        }

        public virtual async Task<string> DoRequestAsync(string endpoint, string method, string body)
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = GetRequest(method, endpoint, body);
            HttpResponseMessage response = await client.SendAsync(request);
            string result = await OnHandleResponseAsync(response);
            return result;
        }


        public virtual async Task<string> OnHandleResponseAsync(HttpResponseMessage response)
        {
            if (response == null)
                throw new HttpResponseException("Unable to get response message", HttpStatusCode.BadRequest);

            if (!response.IsSuccessStatusCode)
            {
                string message = response.Content.ReadAsStringAsync().Result;
                throw new HttpResponseException(message, response.StatusCode);
            }

            return await response.Content.ReadAsStringAsync();
        }
        public virtual async Task<T> GetByIdAsync<T>(string endpoint, string ID)
        {
            string[] splits = ID.Split('_');
            string entitySignatureName = (splits.Length > 1) ? splits[0] : "entity";
            try
            {
                T entity = await DoRequestAsync<T>(String.Format("{0}/{1}", endpoint, ID));
                return entity;
            }
            catch (HttpResponseException)
            {
                string message = String.Format("Unable to find the {0} with ID = {1}", entitySignatureName, ID);
                var resp = new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(message),
                    ReasonPhrase = message
                };

                throw new HttpResponseException(resp);
            }
        }



        #endregion




        /*********************************************************/
        /************************  USER  *************************/
        /*********************************************************/

        #region USER

        /// <summary>
        /// Retrieves a redirection URL containing an authentication 
        /// token for signing a given user into the developer portal.
        /// </summary>
        public async Task<SsoUrl> GenerateSsoURLAsync(string userId)
        {
            string endpoint = String.Format("{0}/users/{1}/generateSsoUrl", _api_endpoint, userId);
            return await DoRequestAsync<SsoUrl>(endpoint, RequestMethod.POST);
        }

        /// <summary>
        /// Create a new user model
        /// </summary>
        public async Task<User> CreateUserAsync(User user)
        {
            string endpoint = String.Format("{0}/users/{1}", _api_endpoint, user.Id);
            await DoRequestAsync<User>(endpoint, RequestMethod.PUT, Utility.SerializeToJson(user));
            return user;
        }

        /// <summary>
        ///  Retrieve a specific user model of a given id
        /// </summary>
        public async Task<User> GetUserAsync(string userId)
        {
            string endpoint = String.Format("{0}/users", _api_endpoint);
            return await GetByIdAsync<User>(endpoint, userId);
        }


        /// <summary>
        /// Delete a specific user model of a given id
        /// </summary>
        public async Task DeleteUserAsync(string userId)
        {
            string endpoint = String.Format("{0}/users/{1}", _api_endpoint, userId);
            await DoRequestAsync<User>(endpoint, RequestMethod.DELETE);
        }

        /// <summary>
        /// Delete user's subscriptions
        /// </summary>
        public async Task DeleteUserWithSubscriptionsAsync(string userId)
        {
            string endpoint = String.Format("{0}/users/{1}?deleteSubscriptions=true", _api_endpoint, userId);
            await DoRequestAsync<User>(endpoint, RequestMethod.DELETE);
        }

        /// <summary>
        /// Retrieve all user models
        /// </summary>
        public async Task<EntityCollection<User>> GetUsersAsync()
        {
            string endpoint = String.Format("{0}/users", _api_endpoint);
            return await DoRequestAsync<EntityCollection<User>>(endpoint);
        }

        /// <summary>
        /// Retrieve a list of subscriptions by the user
        /// </summary>
        public async Task<EntityCollection<Subscription>> GetUserSubscriptionAsync(string userId)
        {
            string endpoint = String.Format("{0}/users/{1}/subscriptions", _api_endpoint, userId);
            return await DoRequestAsync<EntityCollection<Subscription>>(endpoint, RequestMethod.GET);
        }

        /// <summary>
        /// Retrieve a list of groups that the specific user belongs to
        /// </summary>
        public async Task<EntityCollection<Group>> GetUserGroupsAsync(string userId)
        {
            string endpoint = String.Format("{0}/users/{1}/groups", _api_endpoint, userId);
            return await DoRequestAsync<EntityCollection<Group>>(endpoint, RequestMethod.GET);
        }

        /// <summary>
        /// Update a specific user model
        /// </summary>
        public async Task UpdateUserAsync(User user)
        {
            if (String.IsNullOrWhiteSpace(user.Id))
                throw new InvalidEntityException("User's Id is required");
            string endpoint = String.Format("{0}/users/{1}", _api_endpoint, user.Id);
            await DoRequestAsync<User>(endpoint, RequestMethod.PATCH, JsonConvert.SerializeObject(user));
        }

        #endregion






        /*********************************************************/
        /*************************  API  *************************/
        /*********************************************************/

        #region API

        /// <summary>
        /// Creates new API of the API Management service instance.
        /// </summary>
        public async Task<API> CreateAPIAsync(API api)
        {
            string endpoint = String.Format("{0}/apis/{1}", _api_endpoint, api.Id);
            await DoRequestAsync<API>(endpoint, RequestMethod.PUT, Utility.SerializeToJson(api));
            return api;
        }

        /// <summary>
        /// Gets the details of the API specified by its identifier.
        /// </summary>
        public async Task<API> GetAPIAsync(string apiId)
        {
            string endpoint = String.Format("{0}/apis", _api_endpoint);
            return await GetByIdAsync<API>(endpoint, apiId);
        }

        /// <summary>
        /// Deletes the specified API of the API Management service instance.
        /// </summary>
        public async Task DeleteAPIAsync(string apiId)
        {
            string endpoint = String.Format("{0}/apis/{1}", _api_endpoint, apiId);
            await DoRequestAsync<API>(endpoint, RequestMethod.DELETE);
        }

        public async Task UpdateAPIAsync(API api)
        {
            if (String.IsNullOrWhiteSpace(api.Id))
                throw new InvalidEntityException("API's Id is required");
            string endpoint = String.Format("{0}/apis/{1}", _api_endpoint, api.Id);
            await DoRequestAsync<API>(endpoint, RequestMethod.PATCH, JsonConvert.SerializeObject(api));
        }

        /// <summary>
        /// Lists all APIs of the API Management service instance.
        /// </summary>
        public async Task<EntityCollection<API>> GetAPIsAsync()
        {
            string endpoint = String.Format("{0}/apis", _api_endpoint);
            return await DoRequestAsync<EntityCollection<API>>(endpoint, RequestMethod.GET);
        }


        #endregion





        /*********************************************************/
        /******************   API OPERATIONS  ********************/
        /*********************************************************/

        #region API Operations


        /// <summary>
        /// Creates a new operation in the API
        /// </summary>
        public async Task<APIOperation> CreateAPIOperationAsync(string apiId, APIOperation operation)
        {
            string endpoint = String.Format("{0}/apis/{1}/operations/{2}",
                                                _api_endpoint, apiId, operation.Id);
            await DoRequestAsync<APIOperation>(endpoint, RequestMethod.PUT, JsonConvert.SerializeObject(operation));
            return operation;
        }
        public async Task<APIOperation> CreateAPIOperationAsync(API api, APIOperation operation)
        {
            return await CreateAPIOperationAsync(api.Id, operation);
        }

        public async Task UpdateAPIOperationAsync(string apiId, string operationId, APIOperation operation)
        {
            string endpoint = String.Format("{0}/apis/{1}/operations/{2}",
                                                _api_endpoint, apiId, operationId);
            await DoRequestAsync<APIOperation>(endpoint, RequestMethod.PATCH, JsonConvert.SerializeObject(operation));
        }

        /// <summary>
        /// Gets the details of the API Operation specified by its identifier.
        /// </summary>
        public async Task<APIOperation> GetAPIOperationAsync(string apiId, string operationId)
        {
            string endpoint = String.Format("{0}/apis/{1}/operations", _api_endpoint, apiId);
            return await GetByIdAsync<APIOperation>(endpoint, operationId);

        }

        /// <summary>
        /// Lists a collection of the operations for the specified API.
        /// </summary>
        public async Task<EntityCollection<APIOperation>> GetOperationsByAPIAsync(string apiId)
        {
            string endpoint = String.Format("{0}/apis/{1}/operations", _api_endpoint, apiId);
            return await DoRequestAsync<EntityCollection<APIOperation>>(endpoint, RequestMethod.GET);
        }

        /// <summary>
        /// Deletes the specified operation in the API.
        /// </summary>
        public async Task DeleteOperationAsync(string apiId, string operationId)
        {
            string endpoint = String.Format("{0}/apis/{1}/operations/{2}",
                                                _api_endpoint, apiId, operationId);
            await DoRequestAsync<APIOperation>(endpoint, RequestMethod.DELETE);
        }


        #endregion






        /*********************************************************/
        /**********************  PRODUCT  ************************/
        /*********************************************************/

        #region Product

        /// <summary>
        /// Create a product
        /// </summary>
        public async Task<Product> CreateProductAsync(Product product)
        {
            string endpoint = String.Format("{0}/products/{1}", _api_endpoint, product.Id);
            await DoRequestAsync<Product>(endpoint, RequestMethod.PUT, Utility.SerializeToJson(product));
            return product;
        }

        /// <summary>
        /// Gets the details of the product specified by its identifier.
        /// </summary>
        public async Task<Product> GetProductAsync(string productId)
        {
            string endpoint = String.Format("{0}/products", _api_endpoint);
            return await GetByIdAsync<Product>(endpoint, productId);
        }

        /// <summary>
        /// Update a product
        /// </summary>
        public async Task UpdateProductAsync(Product product)
        {
            if (String.IsNullOrWhiteSpace(product.Id))
                throw new InvalidEntityException("Product's Id is required");
            string endpoint = String.Format("{0}/products/{1}", _api_endpoint, product.Id);
            await DoRequestAsync<Product>(endpoint, RequestMethod.PATCH, JsonConvert.SerializeObject(product));
        }


        /// <summary>
        /// Delete a product
        /// </summary>
        /// <param name="productId"></param>
        public async Task DeleteProductAsync(string productId)
        {
            string endpoint = String.Format("{0}/products/{1}?deleteSubscriptions=true", _api_endpoint, productId);
            await DoRequestAsync<Product>(endpoint, RequestMethod.DELETE);
        }


        /// <summary>
        /// Lists a collection of products in the specified service instance.
        /// </summary>
        public async Task<EntityCollection<Product>> GetProductsAsync()
        {
            string endpoint = String.Format("{0}/products", _api_endpoint);
            return await DoRequestAsync<EntityCollection<Product>>(endpoint, RequestMethod.GET);
        }

        /// <summary>
        /// Adds an API to the specified product.
        /// </summary>
        /// <param name="productId"></param>
        /// <param name="api"></param>
        public async Task AddProductAPIAsync(string productId, string apiId)
        {
            string endpoint = String.Format("{0}/products/{1}/apis/{2}",
                                    _api_endpoint, productId, apiId);
            await DoRequestAsync<API>(endpoint, RequestMethod.PUT);
        }

        /// <summary>
        /// Deletes the specified API from the specified product.
        /// </summary>
        /// <param name="productId"></param>
        /// <param name="apiId"></param>
        public async Task DeleteProductAPIAsync(string productId, string apiId)
        {
            string endpoint = String.Format("{0}/products/{1}/apis/{2}",
                                    _api_endpoint, productId, apiId);
            await DoRequestAsync<API>(endpoint, RequestMethod.DELETE);
        }

        /// <summary>
        /// Lists the collection of apis to the specified product.
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        public async Task<EntityCollection<API>> GetProductAPIsAsync(string productId)
        {
            string endpoint = String.Format("{0}/products/{1}/apis",
                                    _api_endpoint, productId);
            return await DoRequestAsync<EntityCollection<API>>(endpoint, RequestMethod.GET);
        }

        /// <summary>
        /// Lists the collection of subscriptions to the specified product.
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        public async Task<EntityCollection<Subscription>> GetProductSubscriptionsAsync(string productId)
        {
            string endpoint = String.Format("{0}/products/{1}/subscriptions",
                                    _api_endpoint, productId);
            return await DoRequestAsync<EntityCollection<Subscription>>(endpoint, RequestMethod.GET);
        }

        /// <summary>
        /// Adds the association between the specified developer group with the specified product.
        /// </summary>
        /// <param name="productId"></param>
        /// <param name="groupId"></param>
        public async Task AddProductGroupAsync(string productId, string groupId)
        {
            string endpoint = String.Format("{0}/products/{1}/groups/{2}",
                                    _api_endpoint, productId, groupId);
            await DoRequestAsync<API>(endpoint, RequestMethod.PUT);

        }

        /// <summary>
        /// Deletes the association between the specified group and product.
        /// </summary>
        /// <param name="productId"></param>
        /// <param name="groupId"></param>
        public async Task DeleteProductGroupAsync(string productId, string groupId)
        {
            string endpoint = String.Format("{0}/products/{1}/groups/{2}",
                                    _api_endpoint, productId, groupId);
            await DoRequestAsync<Group>(endpoint, RequestMethod.DELETE);
        }

        /// <summary>
        /// Lists the collection of developer groups associated with the specified product.
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        public async Task<EntityCollection<Group>> GetProductGroupsAsync(string productId)
        {
            string endpoint = String.Format("{0}/products/{1}/groups",
                                    _api_endpoint, productId);
            return await DoRequestAsync<EntityCollection<Group>>(endpoint, RequestMethod.GET);
        }

        #endregion





        /*********************************************************/
        /**********************  GROUP  **************************/
        /*********************************************************/

        #region Group

        /// <summary>
        /// Create a group
        /// </summary>
        public async Task<Group> CreateGroupAsync(Group group)
        {
            string endpoint = String.Format("{0}/groups/{1}", _api_endpoint, group.Id);
            await DoRequestAsync<Group>(endpoint, RequestMethod.PUT, Utility.SerializeToJson(group));
            return group;
        }

        /// <summary>
        /// Gets the details of the group specified by its identifier.
        /// </summary>
        public async Task<Group> GetGroupAsync(string groupId)
        {
            string endpoint = String.Format("{0}/groups", _api_endpoint);
            return await GetByIdAsync<Group>(endpoint, groupId);
        }

        /// <summary>
        /// Add a user to the specified group
        /// </summary>
        public async Task AddUserToGroupAsync(string groupId, string userId)
        {
            string endpoint = String.Format("{0}/groups/{1}/users/{2}", _api_endpoint, groupId, userId);
            await DoRequestAsync<EntityCollection<User>>(endpoint, RequestMethod.PUT);
        }

        /// <summary>
        /// Remove existing user from existing group.
        /// </summary>
        public async Task RemoveUserFromGroupAsync(string groupId, string userId)
        {

            string endpoint = String.Format("{0}/groups/{1}/users/{2}", _api_endpoint, groupId, userId);
            await DoRequestAsync<EntityCollection<User>>(endpoint, RequestMethod.DELETE);
        }

        /// <summary>
        /// Lists a collection of groups
        /// </summary>
        public async Task<EntityCollection<Group>> GetGroupsAsync()
        {
            string endpoint = String.Format("{0}/groups", _api_endpoint);
            return await DoRequestAsync<EntityCollection<Group>>(endpoint, RequestMethod.GET);
        }

        /// <summary>
        /// Lists a collection of the members of the group, specified by its identifier.
        /// </summary>
        public async Task<EntityCollection<User>> GetUsersInGroupAsync(string groupId)
        {
            string endpoint = String.Format("{0}/groups/{1}/users", _api_endpoint, groupId);
            return await DoRequestAsync<EntityCollection<User>>(endpoint, RequestMethod.GET);
        }

        /// <summary>
        /// Deletes specific group of the API Management
        /// </summary>
        public async Task DeleteGroupAsync(string groupId)
        {
            string endpoint = String.Format("{0}/groups/{1}", _api_endpoint, groupId);
            await DoRequestAsync<Group>(endpoint, RequestMethod.DELETE);
        }
        #endregion






        /*********************************************************/
        /**********************  SUBSCRIPTION  *******************/
        /*********************************************************/


        #region Subscription

        /// <summary>
        /// Creates or updates the subscription of specified user to the specified product.
        /// </summary>
        public async Task<Subscription> CreateSubscriptionAsync(Subscription subscription)
        {
            string endpoint = String.Format("{0}/subscriptions/{1}", _api_endpoint, subscription.Id);
            await DoRequestAsync<Subscription>(endpoint, RequestMethod.PUT, Utility.SerializeToJson(subscription));
            return subscription;
        }

        /// <summary>
        /// Gets the specified Subscription entity.
        /// </summary>
        public async Task<Subscription> GetSubscriptionAsync(string subscriptionId)
        {
            string endpoint = String.Format("{0}/subscriptions", _api_endpoint);
            return await GetByIdAsync<Subscription>(endpoint, subscriptionId);
        }

        /// <summary>
        /// Deletes the specified subscription.
        /// </summary>
        public async Task DeleteSubscriptionAsync(string subscriptionId)
        {
            string endpoint = String.Format("{0}/subscriptions/{1}", _api_endpoint, subscriptionId);
            await DoRequestAsync<Subscription>(endpoint, RequestMethod.DELETE);
        }

        /// <summary>
        /// Updates the details of a subscription specificied by its identifier.
        /// </summary>
        public async Task UpdateSubscriptionAsync(Subscription subscription)
        {
            if (String.IsNullOrWhiteSpace(subscription.Id))
                throw new InvalidEntityException("Subscription's Id is required");
            string endpoint = String.Format("{0}/subscriptions/{1}", _api_endpoint, subscription.Id);
            await DoRequestAsync<Subscription>(endpoint, RequestMethod.PATCH, JsonConvert.SerializeObject(subscription));
        }

        /// <summary>
        /// Lists all subscriptions of the API Management service instance.
        /// </summary>
        public async Task<EntityCollection<Subscription>> GetSubscriptionsAsync()
        {
            string endpoint = String.Format("{0}/subscriptions", _api_endpoint);
            return await DoRequestAsync<EntityCollection<Subscription>>(endpoint, RequestMethod.GET);
        }

        /// <summary>
        /// Gernerate subscription primary key
        /// </summary>
        /// <param name="subscriptionId">Subscription credentials which uniquely identify Microsoft Azure subscription</param>
        public async Task GeneratePrimaryKeyAsync(string subscriptionId)
        {
            string endPoint = String.Format("{0}/subscriptions/{1}/regeneratePrimaryKey", _api_endpoint, subscriptionId);
            await DoRequestAsync<string>(endPoint, RequestMethod.POST);
        }

        /// <summary>
        /// Generate subscription secondary key
        /// </summary>
        /// <param name="subscriptionId">Subscription credentials which uniquely identify Microsoft Azure subscription</param>
        public async Task GenerateSecondaryKeyAsync(string subscriptionId)
        {
            string endPoint = String.Format("{0}/subscriptions/{1}/regenerateSecondaryKey", _api_endpoint, subscriptionId);
            await DoRequestAsync<string>(endPoint, RequestMethod.POST);
        }
        #endregion





        /*********************************************************/
        /**********************  LOGGERs  ************************/
        /*********************************************************/
        #region Loggers

        public async Task<Logger> CreateLoggerAsync(Logger logger)
        {
            string endpoint = String.Format("{0}/loggers/{1}", _api_endpoint, logger.Id);
            await DoRequestAsync<Logger>(endpoint, RequestMethod.PUT, JsonConvert.SerializeObject(logger));
            return logger;
        }

        public async Task<EntityCollection<Logger>> GetLoggersAsync()
        {
            string endpoint = String.Format("{0}/loggers", _api_endpoint);
            return await DoRequestAsync<EntityCollection<Logger>>(endpoint);
        }

        public async Task<Logger> GetLoggerAsync(string loggerId)
        {
            string endpoint = String.Format("{0}/loggers", _api_endpoint, loggerId);
            return await GetByIdAsync<Logger>(endpoint, loggerId);
        }

        public async Task DeleteLoggerAsync(string loggerId)
        {
            string endpoint = String.Format("{0}/loggers/{1}", _api_endpoint, loggerId);
            await DoRequestAsync<Logger>(endpoint, RequestMethod.DELETE);
        }

        #endregion

    }
}