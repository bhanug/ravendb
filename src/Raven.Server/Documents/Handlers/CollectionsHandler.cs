﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Raven.Server.Json;
using Raven.Server.Json.Parsing;
using Raven.Server.Routing;

namespace Raven.Server.Documents
{
    public class CollectionsHandler : DatabaseRequestHandler
    {
        [RavenAction("/databases/*/collections/stats", "GET")]
        public async Task GetCollectionStats()
            {
            RavenOperationContext context;
            using (ContextPool.AllocateOperationContext(out context))
            {
                context.Transaction = context.Environment.ReadTransaction();
                var collections= new DynamicJsonValue();
                var result = new DynamicJsonValue
                {
                    ["NumberOfDocuments"] = DocumentsStorage.GetNumberOfDocuments(context),
                    ["Collections"] = collections
                };

                foreach (var collectionStat in DocumentsStorage.GetCollections(context))
                {
                    collections[collectionStat.Name] = collectionStat.Count;
                }
                var writer = new BlittableJsonTextWriter(context, ResponseBodyStream());
                await context.WriteAsync(writer, result);
                writer.Flush();
            }
        }

        [RavenAction("/databases/*/collections/docs", "GET")]
        public async Task GetCollectionDocuments()
        {
            RavenOperationContext context;
            using (ContextPool.AllocateOperationContext(out context))
            {
                context.Transaction = context.Environment.ReadTransaction();

                var documents = DocumentsStorage.GetDocumentsInReverseEtagOrder(context, GetStringQueryString("name"), GetStart(), GetPageSize());
                await WriteDocumentsAsync(context, documents);
            }
        }

        [RavenAction("/databases/*/collections/docs", "DELETE")]
        public Task DeleteCollectionDocuments()
        {
            var deletedList = new List<long>();
            long totalDocsDeletes = 0;
            RavenOperationContext context;
            var collection = GetStringQueryString("name");
            using (ContextPool.AllocateOperationContext(out context))
            {
                long maxEtag = -1;
                while (true)
                {
                    using (context.Transaction = context.Environment.WriteTransaction())
                    {
                        if (maxEtag == -1)
                            maxEtag = DocumentsStorage.ReadLastEtag(context.Transaction);

                        DocumentsStorage.DeleteCollection(context, collection, deletedList, maxEtag);
                        context.Transaction.Commit();
                    }

                    if (deletedList.Count == 0)
                        break;

                    HttpContext.Response.WriteAsync($"Deleted a batch of {deletedList.Count} documents in {collection}\n");
                    totalDocsDeletes += deletedList.Count;
                    deletedList.Clear();
                }
            }
            HttpContext.Response.WriteAsync($"Deleted a total of {totalDocsDeletes} documents in collection {collection}\n");
            return Task.CompletedTask;
        }
    }
}