import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import StatusBadge from './StatusBadge.vue'

describe('StatusBadge', () => {
  it('renders a readable label for camel-cased statuses', () => {
    const wrapper = mount(StatusBadge, { props: { status: 'UnderReview' } })
    expect(wrapper.text()).toBe('Under review')
    expect(wrapper.attributes('class')).toContain('violet')
  })

  it('renders published as the strongest state', () => {
    const wrapper = mount(StatusBadge, { props: { status: 'Published' } })
    expect(wrapper.text()).toBe('Published')
    expect(wrapper.attributes('class')).toContain('bg-brand-700')
  })
})
