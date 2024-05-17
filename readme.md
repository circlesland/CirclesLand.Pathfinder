# pathfinder2-updater

## Quickstart

The repository contains a docker-compose file that starts a pathfinder2 instance and the pathfinder2-updater.
It requires a connection string to a postgres db that contains the indexer data.
The db for v1 is from the [blockchain-indexer](https://github.com/CirclesUBI/blockchain-indexer) and for v2 from
the [circles-nethermind-plugin](https://github.com/CirclesUBI/circles-nethermind-plugin).

### Configure

Create a `.env` file in the root of the repository and your connection string.
Also specify the version of the circles implementation you want to use.

```
INDEXER_DB_CONNECTION_STRING=Server=localhost;Port=5432;Database=postgres;User Id={user};Password={password};
CIRCLES_VERSION=v1 # or v2
```

### Start the environment

On first start, it pulls the pathfinder2 image and builds the pathfinder2-updater image.

```shell
docker-compose up
```

When running, the pathfinder2-updater will query the trust graph and balances from the index db and send
a binary dump of that data to the pathfinder2 instance. The two services share a volume where the dump is stored.
The pathfinder loads the dump on `load_safes_binary` rpc call.

## Dependencies

To run the _pathfinder2-updater_ with the instructions in this readme, you'll need:

* A running [pathfinder2](https://github.com/chriseth/pathfinder2#using-the-server) instance listening to json-rpc
  requests on port 54389
* A running [blockchain-indexer](https://github.com/circlesland/blockchain-indexer) instance that announces new
  transactions on port 8675
    * with a postgres db

## Start the pathfinder2-updater

1) Start pathfinder2
2) Start blockchain-indexer
3) Start the updater:  
   _replace the values in curly braces_

```shell
docker run \
  --network=host \
  -v ".state/.pathfinder2:/var/pathfinder2/data" \
  --rm \
  circlesubi/pathfinder2-updater:dev \
  "v1" \
  "Server={server};Port={port};Database=indexer;User ID={username};Password={password};Command Timeout=240" \
  ws://localhost:8675 \
  "/var/pathfinder2/data/capacity_graph.db" \
  ".state/.pathfinder2/capacity_graph.db" \
  http://localhost:54389
```

where the command line parameters are:

Docker arguments:  
[0]: use the host network (to reach the previously started pathfinder2)  
[1]: use a volume that's accessible to the pathfinder2  
[2]: delete the container after it's stopped  
[3]: the image to run

Pathfinder2-updater arguments:
[4]: Which circles version to use (v1 or v2)
[5]: indexer db connection string (see: https://github.com/circlesland/blockchain-indexer)    
[6]: indexer websocket endpoint (see: https://github.com/circlesland/blockchain-indexer)  
[7]: filesystem path where the initial capacity graph dump should be stored (pathfinder2 needs read access so this has
to be a volume)    
[8]: http json-rpc endpoint of pathfinder2 (see: https://github.com/chriseth/pathfinder2)
