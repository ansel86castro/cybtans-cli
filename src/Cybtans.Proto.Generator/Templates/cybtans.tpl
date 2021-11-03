{
    "Service": "@{SERVICE}",
    "Steps":[
        {
          "Type": "messages",
          "Output": ".",
          "ProtoFile": "./Proto/Data.proto",
          "AssemblyFile": "./@{SERVICE}.Data/bin/Debug/net5.0/@{SERVICE}.Data.dll"       
        },
        {
            "Type": "proto",
            "Output": ".",
            "ProtoFile": "./Proto/@{SERVICE}.proto",
            "Models": {                            
              "UseCytansSerialization": true
            },
            "Services": {            
                "Namespace": "@{SERVICE}.Services"
            },
            "Controllers": {
                "UseActionInterceptor": false,
                "Namespace": "@{SERVICE}.WebApi.Controllers"
            },
            "CSharpClients": {
               "Generate": true,
               "Namespace": "@{SERVICE}.Clients"
            }
        }
    ]
}