//@ts-check

/// <reference no-default-lib="true"/>
/// <reference lib="esnext" />
/// <reference lib="webworker" /> 

class ServiceWorkerController {
    initialize() {
        // @ts-ignore
        self.addEventListener("fetch", this.#handleFetch);
    }

    /**
     * @param {FetchEvent} event
     */
    #handleFetch(event) {
        event.respondWith(fetch(event.request));
    }
}

let controller = new ServiceWorkerController();
controller.initialize();