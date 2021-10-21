syntax = "proto3";

import "./Data.proto";

package @{SERVICE};

service @{SERVICE}Service {
	option (prefix) = "api/@{SERVICE}";
	option (description) = "@{SERVICE}";

	rpc Hello(HelloRequest) returns (HelloReply){
		option (method) = "GET";
		option template = "hello";
		option (description) = "Say Hello";		
	}
}

message HelloRequest {
	string msg = 1;
}

message HelloReply {
	string response = 1;
}