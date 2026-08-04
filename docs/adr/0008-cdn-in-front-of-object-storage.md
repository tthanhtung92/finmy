# ADR-0008: Serve receipt images with presigned URLs, with a CDN in front of the object-storage origin

## Status

Accepted, 2026-07-23

## Context

The upload direction was already built: receipt images go to an object store (MinIO locally, S3 or Cloudflare R2 in production) over the S3 API, and Postgres holds a `Receipt` row pointing at the object by its key. The bucket is private because receipts belong to a specific Space.

The read direction is the question here: given a `Receipt`, how does a client see the image again? The design has to keep the bucket private, avoid turning the app into a bandwidth bottleneck, and line up with the production shape where a CDN sits in front of the origin.

## Options considered

**Make the bucket publicly readable.** Every object gets a stable URL, which a CDN caches easily at the edge because the URL never changes. But anyone who guesses the path can view the file, which is unacceptable for private data.

**Proxy the bytes through the app.** The app fetches the object from the origin and streams it back, with a response cache in between if wanted. Control is tight, since the app sits in every request, but it also carries the bandwidth for every image for every user, which is precisely the job a CDN exists to take over.

**Presigned URL plus a 302 redirect, caching the object key.** The app looks up the object key (through HybridCache, cache-aside), signs a time-limited presigned URL, and redirects the client to the origin with a `302`; the client fetches the bytes itself. The bucket stays private and the app never touches an image byte.

## Decision

Presigned URL plus redirect. `GET /receipts/{id}` caches the object key through HybridCache, signs a fresh presigned URL on every request, and answers `302` with `Cache-Control: private`.

Two points shape the implementation.

**Cache the object key, not the presigned URL.** A receipt's object key is stable, since `Receipt` is immutable, so it caches well. The presigned URL expires (`PresignedUrlLifetimeMinutes`), so it has to be signed fresh from the cached key on each request. Caching a presigned URL for longer than its own lifetime hands out dead links, a bug that works correctly for a few minutes before it starts failing.

**Changing the endpoint changes the origin.** The code speaks the S3 API through `AWSSDK.S3`, and MinIO differs from real S3 or R2 only in `ServiceURL`. In production a real object store becomes the origin with a CDN such as Cloudflare in front, and the serving code does not change.

## Consequences

What runs today is origin-direct: the client goes straight to the origin through the presigned URL, with no edge layer in between. That follows from choosing presigned URLs; it is not an oversight waiting to be patched.

Presigned URLs and edge caching are in tension. A CDN caches by URL, and a presigned URL changes on every signing, so the edge cannot cache the object through the presigned path. Cloudflare R2 states plainly that presigned URLs work on the storage domain while caching works on a custom domain. Getting both privacy and edge caching means moving the signing up to the CDN layer: CloudFront signed URLs or signed cookies with Origin Access Control (the object sits behind the CDN at a stable path so the edge can cache it, and the CDN's signing mechanism decides who may view it), or a Cloudflare custom domain with a Worker. That is the production evolution, recorded here so nobody mistakes today's MinIO slice for something that already has an edge layer.

`Cache-Control` has to be `private` because receipts are personalised: a shared cache holding the redirect and handing it to someone else is a leak. `no-store` is the default so there is nothing to get wrong. If browser caching of the redirect is worth optimising with `max-age`, that value must stay below the presigned URL's lifetime, or the browser follows a cached redirect to an expired URL.

`GET /receipts/{id}` has no authorization yet. The presigned URL keeps the bucket private, but anyone who calls the endpoint gets a signature. Once Space and Member exist, this is where the caller has to be checked against the Space that owns the receipt before signing.

One trade-off carries over from the upload work: uploading bytes before storing the pointer leaves an orphaned object if the database commit fails. That is outside this record's scope and is handled alongside Ledger's concurrency work.
