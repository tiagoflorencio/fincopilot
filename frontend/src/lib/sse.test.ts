import { describe, expect, it } from 'vitest'
import { createSseParser } from './sse'

describe('createSseParser', () => {
  it('junta eventos que chegam quebrados em vários chunks', () => {
    const received: string[] = []
    const feed = createSseParser((d) => received.push(d))

    feed('data: {"type":"ite')
    feed('ration","iteration":1}\n')
    expect(received).toHaveLength(0)

    feed('\ndata: {"type":"done"}\n\n')
    expect(received).toEqual(['{"type":"iteration","iteration":1}', '{"type":"done"}'])
  })

  it('aceita CRLF e ignora linhas que não são data', () => {
    const received: string[] = []
    const feed = createSseParser((d) => received.push(d))

    feed(': keep-alive\r\n\r\nevent: x\r\ndata: oi\r\n\r\n')
    expect(received).toEqual(['oi'])
  })
})
