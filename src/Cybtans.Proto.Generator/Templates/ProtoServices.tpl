
// **************************** START @{ENTITY} **********************************************

message Get@{ENTITY}Request {
	@{ID_TYPE} @{ID} = 1;
}

message Update@{ENTITY}Request {
	@{ID_TYPE} @{ID} = 1;
	@{ENTITYDTO} value = 2 [(ts).partial = true];
}

message Delete@{ENTITY}Request{
	@{ID_TYPE} @{ID} = 1;
}

message GetAll@{ENTITY}Response {
	repeated @{ENTITYDTO} items = 1;
	int64 page = 2;
	int64 totalPages = 3;
	int64 totalCount = 4;
}

message Create@{ENTITY}Request {
	@{ENTITYDTO} value = 1 [(ts).partial = true];
}

service @{ENTITY}Service {
	option (prefix) ="api/@{ENTITY}";

	rpc GetAll(GetAllRequest) returns (GetAll@{ENTITY}Response){		
		option (method) = "GET";
		@{GetAll_OPTIONS}
	};

	rpc Get(Get@{ENTITY}Request) returns (@{ENTITYDTO}){	
		option (template) = "{@{ID}}"; 
		option (method) = "GET";
		@{Get_OPTIONS}
	};

	rpc Create(Create@{ENTITY}Request) returns (@{ENTITYDTO}){			
		option (method) = "POST";
		@{Create_OPTIONS}
	};

	rpc Update(Update@{ENTITY}Request) returns (@{ENTITYDTO}){			
		option (template) = "{@{ID}}"; 
		option (method) = "PUT";
		@{Update_OPTIONS}
	};

	rpc Delete(Delete@{ENTITY}Request) returns (void){
		option (template) = "{@{ID}}"; 
		option (method) = "DELETE";
		@{Delete_OPTIONS}
	};
}

// **************************** END @{ENTITY} **********************************************