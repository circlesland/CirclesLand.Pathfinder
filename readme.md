# pathfinder2-updater

## Quickstart

The repository contains a docker-compose file that starts a pathfinder2 instance and the pathfinder2-updater.
It requires a connection string to a postgres db that contains the indexer data.
The db for v1 is from the [blockchain-indexer](https://github.com/CirclesUBI/blockchain-indexer) and for v2 from
the [circles-nethermind-plugin](https://github.com/CirclesUBI/circles-nethermind-plugin).

### Configure

Create a `.env` file in the root of the repository and add your connection string.
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

### Dependencies

The updater depends on the following services:

* A running [pathfinder2](https://github.com/chriseth/pathfinder2#using-the-server) instance listening to json-rpc
  requests
* A running [blockchain-indexer](https://github.com/circlesland/blockchain-indexer) instance that announces new
  transactions via websocket  
  ... or a running Nethermind node
  with [circles-nethermind-plugin](https://github.com/CirclesUBI/circles-nethermind-plugin) for v2 support
* A postgres db that contains the indexer data
