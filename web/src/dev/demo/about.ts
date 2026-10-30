import { devLicenses } from '../../licenses'
import { params, wait } from './core'

/** A short stand-in for the PC app's /licenses.txt (LICENSE + THIRD_PARTY_NOTICES.md). */
const DEMO_LICENSES = `MIT License

Copyright (c) 2026 Demo Author

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the Software without restriction.


# Third-party notices

League Remote is MIT licensed (see LICENSE). It bundles or depends on the software below.

## Web app (npm)

| Package | License |
|---|---|
| vue | MIT |
| @microsoft/signalr | MIT |
`

/**
 * Settings › About › Licenses in every demo: the text after a short delay; `&licenses=fail` shows the "Couldn't Load"
 * state (its Try Again fails again).
 */
export function demoLicenses() {
  devLicenses.load = async () => {
    await wait(500)
    return params.get('licenses') === 'fail' ? null : DEMO_LICENSES
  }
}
