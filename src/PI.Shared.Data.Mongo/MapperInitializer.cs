using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoMapper;
using Crochik.Mongo;
using Crochik.Mongo.Conventions;
using Microsoft.AspNetCore.DataProtection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo
{
    // http://mongodb.github.io/mongo-csharp-driver/2.0/reference/bson/mapping/

    public class CustomSerializationProvider : IBsonSerializationProvider
    {
        private static readonly DecimalSerializer DecimalSerializer = new(BsonType.Decimal128, new RepresentationConverter(false, true));
        private static readonly NullableSerializer<decimal> NullableSerializer = new(new DecimalSerializer(BsonType.Decimal128, new RepresentationConverter(false, true)));

        public IBsonSerializer GetSerializer(Type type)
        {
            if (type == typeof(decimal))
            {
                return DecimalSerializer;
            }

            if (type == typeof(decimal?))
            {
                return NullableSerializer;
            }

            return null; // falls back to Mongo defaults
        }
    }

    public class MapperInitializer : MongoConnection.IRegisterClassMap
    {
        private readonly EncryptedStringSerializer _encryptStringSerializer;
        private readonly OptionalMagicGuidSerializer _optionalMagicGuidSerializer = new();
        private readonly MagicGuidSerializer _guidSerializer = new();

        public MapperInitializer(IDataProtectionProvider provider)
        {
            DataProtectorCache.Init(provider);

            var _protector = provider.CreateProtector(typeof(MapperInitializer).FullName);
            _encryptStringSerializer = new EncryptedStringSerializer(_protector);
        }

        public void Register(MongoConnection connection)
        {
            ConventionRegistry.Register("Custom Conventions", new ConventionPack
            {
                new IgnoreIfNullConvention(true),
                new EnumRepresentationConvention(BsonType.String),
                new IgnoreExtraElementsConvention(true),
            }, t =>
            {
                // if (t == typeof(BsonDocument) || t == typeof(ExpandoObject) || t == typeof(object))
                // {
                //     return false;
                // }

                return true;
            });
            
            // this will prevent any property of type object to use polymorphism  :( 
            // ... and it will fail if we try to save a null object 
            //      with: "C# null values of type 'BsonValue' cannot be serialized using a serializer of type 'BsonValueSerializer'."
            BsonSerializer.RegisterDiscriminatorConvention(typeof(object), IgnoreDiscriminatorConvention.Instance);

            // the default serializer for <object> is a ObjectSerializer 
            // the custom will do some magic mapping  
            BsonSerializer.RegisterSerializer(new CustomObjectSerializer());

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var types = assembly.GetTypes().Where(x => x.GetCustomAttribute(typeof(DiscriminatorWithFallbackAttribute), true) != null);
                foreach (var type in types)
                {
                    BsonSerializer.RegisterDiscriminatorConvention(type, FlexDiscriminatorConvention.Instance);
                }
            }

            // can user provider instead of individuals but...
            // BsonSerializer.RegisterSerializationProvider(new CustomSerializationProvider());

            BsonSerializer.RegisterSerializer(
                typeof(Guid),
                _guidSerializer
            );

            BsonSerializer.RegisterSerializer(
                typeof(Guid?),
                _optionalMagicGuidSerializer
            );

            // HACK to avoid deserialization of Double, no idea of why it broke
            // trying to deserialize this breaks otherwise
            /*
                "latitude" : {
                    "_t" : "System.Single",
                    "_v" : 41.6842918395996
                },
                "longitude" : {
                    "_t" : "System.Single",
                    "_v" : -81.3266677856445
                },
            */
            BsonSerializer.RegisterSerializer(typeof(System.Single), new SingleSerializer(BsonType.Double, new RepresentationConverter(false, true)));

            // serialize decimals as "decimal" instead of string
            BsonSerializer.RegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128, new RepresentationConverter(false, true)));
            BsonSerializer.RegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128, new RepresentationConverter(false, true))));

            var dictSerializer = new DictionaryInterfaceImplementerSerializer<Dictionary<string, object>>(DictionaryRepresentation.Document)
                .WithValueSerializer(new CustomObjectSerializer());

            BsonSerializer.RegisterSerializer(typeof(Dictionary<string, object>), dictSerializer);

            BsonClassMap.RegisterClassMap<PI.Shared.Models.ExternalIdentity>(cm =>
            {
                cm.AutoMap();
                cm.MapMember(c => c.Claims)
                    .SetSerializer(
                        new DictionaryInterfaceImplementerSerializer<Dictionary<string, string>>(DictionaryRepresentation.ArrayOfDocuments)
                    );
                // cm.MapMember(c => c.Provider).SetSerializer(new EnumSerializer<Shared.Models.ExternalProvider>(BsonType.String));

                // already part of the identity
                cm.UnmapMember(c => c.ExternalId);
                cm.UnmapMember(c => c.Provider);
            });

            BsonClassMap.RegisterClassMap<Token>(cm =>
            {
                cm.AutoMap();
                cm.MapMember(c => c.AccessToken).SetSerializer(_encryptStringSerializer);
                cm.MapMember(c => c.RefreshToken).SetSerializer(_encryptStringSerializer);
            });

            BsonClassMap.RegisterClassMap<Data.Models.LeadAggregation.Row>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });
            
            RegisterActionOptions();
            RegisterAppConfig();
            RegisterActors();

            // integrations
            RegisterIntegrationConfiguration();
            BsonClassMap.RegisterClassMap<PI.Shared.Models.EntityIntegration>(cm =>
            {
                cm.AutoMap();
                cm.MapMember(c => c.Authentication).SetSerializer(_encryptStringSerializer);
            });

            RegisterChildren<Messages.Flow.ActionOptions>();

            // automapper
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddMaps(typeof(MapperInitializer).Assembly);

                // for when the models are moved to models, add profiles 
                cfg.AddMaps(typeof(Shared.Models.Model).Assembly);
            });
            config.AssertConfigurationIsValid();

            connection.Mapper = new Mapper(config);
        }

        private void RegisterActors()
        {
            BsonClassMap.RegisterClassMap<Shared.Models.Actor>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminatorIsRequired(true);
            });

            RegisterChildren<Shared.Models.Actor>();
        }

        private void RegisterAppConfig()
        {
            BsonClassMap.RegisterClassMap<Form.Models.Page>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminatorIsRequired(true);
            });
            RegisterChildren<Form.Models.Page>();

            BsonClassMap.RegisterClassMap<Form.Models.MenuItem>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminatorIsRequired(true);
            });
            RegisterChildren<Form.Models.MenuItem>();

            // can't add because it is an interface
            // BsonClassMap.RegisterClassMap<Form.Models.FieldOptions>(cm =>
            // {
            //     cm.AutoMap();
            //     cm.SetDiscriminatorIsRequired(true);
            // });            

            RegisterChildren<Form.Models.FieldOptions>();

            // TODO: delete ?
            // I assume this is not needed anymore since just some fields are here :(
            BsonClassMap.RegisterClassMap<Form.Models.FormField>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminatorIsRequired(true);
            });
            BsonClassMap.RegisterClassMap<Form.Models.SelectField>(cm =>
            {
                cm.AutoMap();
                cm.UnmapField(x => x.SelectFieldOptions);
            });
            BsonClassMap.RegisterClassMap<Form.Models.LookupField>(cm =>
            {
                cm.AutoMap();
                cm.UnmapField(x => x.LookupFieldOptions);
            });
            BsonClassMap.RegisterClassMap<Form.Models.TextField>(cm =>
            {
                cm.AutoMap();
                cm.UnmapField(x => x.TextFieldOptions);
            });
            BsonClassMap.RegisterClassMap<Form.Models.DictionaryField>(cm =>
            {
                cm.AutoMap();
                cm.UnmapField(x => x.DictionaryFieldOptions);
            });
            BsonClassMap.RegisterClassMap<Form.Models.NumberField>(cm =>
            {
                cm.AutoMap();
                cm.UnmapField(x => x.NumberFieldOptions);
            });

            RegisterChildren<Form.Models.FormField>();
        }

        private void RegisterIntegrationConfiguration()
        {
            // discriminator for ActionOptions
            BsonClassMap.RegisterClassMap<IntegrationConfiguration>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminatorIsRequired(true);
            });

            RegisterChildren<IntegrationConfiguration>();
        }
        
        private void RegisterActionOptions()
        {
            // discriminator for ActionOptions
            BsonClassMap.RegisterClassMap<Messages.Flow.ActionOptions>(cm =>
            {
                cm.AutoMap();
                cm.SetDiscriminatorIsRequired(true);
            });

            RegisterChildren<Messages.Flow.ActionOptions>();
        }

        private void RegisterChildren<T>()
        {
            foreach (Type type in
                     Assembly.GetAssembly(typeof(T))
                         .GetTypes()
                         .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(T))))
            {
                BsonClassMap.LookupClassMap(type);
            }
        }
    }
}