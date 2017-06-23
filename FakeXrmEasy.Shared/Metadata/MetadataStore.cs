using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Metadata;

namespace FakeXrmEasy.Metadata
{
    internal class MetadataStore : Dictionary<string, EntityMetadata>
    {
        private static bool initializedFromAssembly = false;

        public void InitializeFromAssembly(Assembly assembly)
        {
            if (initializedFromAssembly)
            {
                return;
            }

            var entities = GetEntitiesFromAssembly(assembly);

            foreach (var entityMetadata in entities)
            {
                this.AddEntityMetadata(entityMetadata);
            }
        }

        public void AddEntityMetadata(EntityMetadata entityMetadata)
        {
            if (!this.ContainsKey(entityMetadata.LogicalName))
            {
                this[entityMetadata.LogicalName] = entityMetadata;
            }
            else
            {
                // Merge
            }
        }

        public EntityMetadata GetEntityMetadata(string logicalName)
        {
            if (this.ContainsKey(logicalName))
            {
                return this[logicalName];
            }

            return null;
        }

        private static EntityMetadata[] GetEntitiesFromAssembly(Assembly assembly, string logicNameFilter = null)
        {
            var entityTypes = assembly.GetTypes()
                .Where(t => typeof(Entity).IsAssignableFrom(t))
                .Where(t => t.GetCustomAttributes<EntityLogicalNameAttribute>(true).Any())
                .Where(t => !string.IsNullOrWhiteSpace(t.GetCustomAttributes<EntityLogicalNameAttribute>(true).First().LogicalName))
                .ToDictionary(t => t.GetCustomAttributes<EntityLogicalNameAttribute>(true).First().LogicalName, t => t, StringComparer.OrdinalIgnoreCase)
                .Where(e => logicNameFilter == null || e.Key == logicNameFilter);

            var entities = new List<EntityMetadata>();

            foreach (var entityInfo in entityTypes)
            {
                var entityMetadata = new EntityMetadata
                {
                    MetadataId = Guid.NewGuid(),
                    LogicalName = entityInfo.Key
                };

                var attributeMetadataCollection = GetAttributesFromEntityType(entityInfo);
                entityMetadata.GetType().GetProperty("Attributes")?.SetValue(entityInfo, attributeMetadataCollection, null);

                entities.Add(entityMetadata);
            }

            return entities.ToArray();
        }

        private static AttributeMetadata[] GetAttributesFromEntityType(KeyValuePair<string, Type> entityInfo)
        {
            var attributeTypes = entityInfo.Value
                .GetProperties()
                .Where(pi => pi.GetCustomAttributes<AttributeLogicalNameAttribute>(true).Any())
                .ToDictionary(pi => pi.GetCustomAttributes<AttributeLogicalNameAttribute>(true).First().LogicalName, pi => pi.PropertyType);

            var attributeMetadataCollection = new List<AttributeMetadata>();

            foreach (var attributeInfo in attributeTypes)
            {
                AttributeMetadata attributeMetadata = null;

                switch (attributeInfo.Value.Name)
                {
                    case "String":
                        attributeMetadata = new StringAttributeMetadata();
                        break;

                    case "EntityReference":
                        attributeMetadata = new LookupAttributeMetadata();
                        break;

                    case "OptionSetValue":
                        attributeMetadata = new PicklistAttributeMetadata();
                        break;

                    case "Money":
                        attributeMetadata = new MoneyAttributeMetadata();
                        break;

                    case "Nullable`1":
                        switch (attributeInfo.Value.GetGenericArguments()[0].Name)
                        {
                            case "Int32":
                                attributeMetadata = new IntegerAttributeMetadata();
                                break;

                            case "Double":
                                attributeMetadata = new DoubleAttributeMetadata();
                                break;

                            case "Boolean":
                                attributeMetadata = new BooleanAttributeMetadata();
                                break;

                            case "Decimal":
                                attributeMetadata = new DecimalAttributeMetadata();
                                break;

                            case "DateTime":
                                attributeMetadata = new DateTimeAttributeMetadata();
                                break;

                            case "Guid":
                                attributeMetadata = new LookupAttributeMetadata();
                                break;

                            case "Int64":
                                attributeMetadata = new BigIntAttributeMetadata();
                                break;

                            case "statecode":
                                attributeMetadata = new StateAttributeMetadata();
                                break;

                            default:
                                if (attributeInfo.Value.GetGenericArguments()[0].BaseType == typeof(Enum))
                                {
                                    attributeMetadata = new StateAttributeMetadata();
                                }

                                break;
                        }
                        break;

#if FAKE_XRM_EASY_2015 || FAKE_XRM_EASY_2016 || FAKE_XRM_EASY_365
                    case "Guid":
                        attributeMetadata = new UniqueIdentifierAttributeMetadata();
                        break;
#endif
#if !FAKE_XRM_EASY
                    case "Byte[]":
                        attributeMetadata = new ImageAttributeMetadata();
                        break;
#endif

                    default:
                        if (attributeInfo.Value.BaseType == typeof(Entity))
                        {
                            attributeMetadata = new LookupAttributeMetadata();
                        }

                        break;
                }

                if (attributeMetadata == null)
                {
                    continue;
                }

                attributeMetadata.LogicalName = attributeInfo.Key;
                attributeMetadata.MetadataId = Guid.NewGuid();
                attributeMetadata.GetType().GetProperty("EntityLogicalName")?.SetValue(attributeMetadata, entityInfo.Key, null);
                attributeMetadataCollection.Add(attributeMetadata);
            }

            return attributeMetadataCollection.ToArray();
        }
    }
}
